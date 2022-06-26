using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

namespace AttackSetting
{
    /// <summary>
    /// 攻撃のエフェクトのタイプ
    /// </summary>
    public enum AttackEffect
    {
        CameraShake,
        ControllerShake,
        ZoomIn,
        ZoomOut,
        SetEffect,
    }

    public partial class ActionCtrl : MonoBehaviour
    {

        [SerializeField]
        AnimationCtrl _animCtrl;
        [SerializeField]
        List<ComboData> _comboDatas = new List<ComboData>();
        public List<ComboData> CurrentAttacks => _comboDatas;
        
        [SerializeField]
        List<ActionData> _attacks = new List<ActionData>();
        [SerializeField]
        NewHitCtrl _hitCtrl;

        AttackType _prevType;
        ComboData _currentCombo;
       
        public float ReceiveTimer { get; private set; } = 0.0f;
        public float KeepTimer { get; private set; } = 0.0f;
        int _comboCount = 0;

        public ActionData CurrentAction { get; private set; }
        public bool ReserveAction { get; private set; } = false;
        public bool InReceiveTime { get; private set; } = false;

        public bool ActionKeep { get; private set; } = false;

        public bool ComboEnd { get; private set; } = false;

        /// <summary>
        /// 攻撃のアニメーションクリップの名前
        /// </summary>
        enum ClipName
        {
            First,
            Second,
        }
        
        ClipName _clipName = ClipName.First;

        void Start()
        {
            if (!_animCtrl) _animCtrl = GetComponentInChildren<AnimationCtrl>();
            if (!_hitCtrl) _hitCtrl = GetComponentInChildren<NewHitCtrl>();

            _hitCtrl.SetUp(this);
        }
        void Update()
        {
            if (KeepTimer > 0.0f)
            {
                KeepTimer -= Time.deltaTime;

                ActionKeep = true;
                ComboEnd = false;
                if (KeepTimer <= 0.0f)
                {
                    KeepTimer = 0.0f;
                    ActionKeep = false;
                    ReserveAction = false;
                }
            }
            else if (ReceiveTimer > 0.0f)
            {
                InReceiveTime = true;
                ReceiveTimer -= Time.deltaTime;

                if (ReceiveTimer <= 0.0f)
                {
                    ReceiveTimer = 0.0f;
                    InReceiveTime = false;
                }
            }

            if (!ReserveAction && ReceiveTimer <= 0.0f)
            {
                _comboCount = 0;
            }

        }
        /// <summary>
        /// アクションを選定する
        /// </summary>
        /// <param name="attackType"></param>
        /// <param name="id"></param>
        public void RequestAction(AttackType attackType, int id = 0)
        {
            ReserveAction = true;
            //if (!_comboDatas.Any()) return;
            ActionData data = null;
            if (_prevType != attackType) _comboCount = 0;
            _prevType = attackType;

            //for (int i = 0; i < _attacks.Count; i++)
            //{
            //    if (_attacks[i].AttackType != attackType) continue;
            //    if (_attacks[i].Id != id) continue;

            //    data = _attacks[i];
            //    _comboCount = i;

            //    break;
            //}

            #region
            switch (attackType)
            {
                case AttackType.Weak:
                    _currentCombo = _comboDatas[0];
                    data = _comboDatas[0].ActionDatas[_comboCount];
                    break;

                case AttackType.Airial:
                    if (_comboDatas.Count <= 1) break;
                    _currentCombo = _comboDatas[1];
                    data = _comboDatas[1].ActionDatas[_comboCount];
                    break;
                case AttackType.Counter:
                    data = _comboDatas[0].ActionDatas[-1];

                    break;
                case AttackType.Heavy:

                    break;
                case AttackType.Launch:

                    break;
                default:
                    break;
            }
            #endregion

            //if (data) SetAction(_attacks[_comboCount]);
            if (data) SetAction(data);
            _comboCount++;
            if (_comboCount > _currentCombo.ComboCount)
            {
                _comboCount = 0;
                ComboEnd = true;
            }
        }

        public void EndAttack()
        {
            TriggerOnDisable();
            KeepTimer = 0.0f;
            ReceiveTimer = 0.0f;
            _comboCount = 0;
        }
        /// <summary>
        /// アクションをセットする関数
        /// </summary>
        /// <param name="attack">攻撃データ</param>
        void SetAction(ActionData attack)
        {
            
            CurrentAction = attack;
            ReceiveTimer = attack.ReceiveTime;
            KeepTimer = attack.KeepTime;

            _animCtrl.ChangeClip(_clipName.ToString(), attack.AnimSet.Clip);
            //if (attack.UseRootMotion) _animCtrl.SetRootAnim();

            _animCtrl.Play(_clipName.ToString(), attack.AnimSet.Duration);

            if (_clipName is ClipName.Second)　//ブレンドするために二つのステートを交互に再生する
                _clipName = ClipName.First;
            else
                _clipName = ClipName.Second;
        }

        /// <summary>
        /// 攻撃ヒット時の関数
        /// </summary>
        /// <param name="target">ヒットしたコライダー</param>
        public void HitCallBack(Collider target)
        {
            target.gameObject.GetComponent<IDamage>()?.AddDamage(CurrentAction.Damage);
            EffectManager.HitStop(CurrentAction.HitStopPower);
            if (CurrentAction.Effect.HitEff)
                EffectManager.PlayEffect(CurrentAction.Effect.HitEff, target.ClosestPoint(transform.position));

        }
        /// <summary>
        /// 攻撃の当たり判定を出すアニメーションイベント用関数
        /// </summary>
        private void TriggerOnEnable()
        {
            _hitCtrl.TriggerOnEnable();
        }
        /// <summary>
        /// 攻撃の当たり判定を消すアニメーションイベント用関数
        /// </summary>
        private void TriggerOnDisable()
        {
            _hitCtrl.TriggerOnDisable();
        }
    }
}