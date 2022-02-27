﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using System;
[RequireComponent(typeof(CharacterController), typeof(AttackAssistController))]
public partial class PlayerStateMachine : MonoBehaviour, IDamage
{
    #region parameter
    [Tooltip("移動スピード")]
    float m_moveSpeed = default;
    [Tooltip("ダッシュの速さ")]
    [SerializeField] float m_dashSpeed = 10f;
    [Tooltip("歩くスピード")]
    [SerializeField] float m_walkSpeed = 6f;
    [Tooltip("playerの振り向きスピード")]
    [SerializeField] float m_rotateSpeed = 10f;
    [Tooltip("重力の大きさ")]
    [SerializeField] float m_gravityScale = 10f;
    [Tooltip("ジャンプ力")]
    [SerializeField] float m_jumpPower = 10f;
    [Tooltip("ジャスト回避の無敵時間")]
    [SerializeField] float m_justTime = 0.3f;
    [Tooltip("回避移動のスピード")]
    [SerializeField] float m_avoidSpeed = 1.2f;
    [Tooltip("回避移動の時間")]
    [SerializeField] float m_avoidEndTime = 0.6f;
    [Tooltip("接地判定での中心からの距離")]
    [SerializeField] float m_isGroundLength = 1.05f;
    [Tooltip("接地判定の範囲")]
    [SerializeField] float m_isGroundRadius = 0.18f;
    [Tooltip("地面のレイヤー")]
    [SerializeField] LayerMask m_groundLayer;
    #endregion

    #region status
    [Tooltip("プレイヤーの体力")]
    [SerializeField]
    private IntReactiveProperty m_hp = new IntReactiveProperty(100);
    [Tooltip("プレイヤーの最大体力")]
    [SerializeField]
    private IntReactiveProperty m_maxHp = new IntReactiveProperty(100);
    public IReadOnlyReactiveProperty<int> OnDamage => m_hp;
    public IReadOnlyReactiveProperty<int> MaxHp => m_maxHp;

    #endregion

    #region Event
    /// <summary>ジャスト回避のイベント</summary>
    private Subject<Unit> justsubject = new Subject<Unit>();
    public IObservable<Unit> OnJust => justsubject;
    /// <summary> プレイヤーが死んだ時のイベント</summary>
    private Subject<Unit> playerDeath = new Subject<Unit>();
    public IObservable<Unit> PlayerDeath => playerDeath;
    #endregion

    Transform m_selfTrans;
    CharacterController m_controller;
    Animator m_anim;
    AttackAssistController m_attackAssist;
    InputManager m_inputManager;
    WeaponHolder m_weaponHolder;
    HitCtrl m_hitCtrl;
    [SerializeField] ActionControl m_actionCtrl;
    [SerializeField] AnimationCtrl m_animCtrl;


    float m_currentRotateSpeed = 10f;
    float m_currentJustTime = default;
    float m_currentGravityScale = default;
    /// <summary>ジャスト回避のトリガー</summary>
    bool m_justTrigger = false;

    #region State
    PlayerStateBase m_currentState;
    IdleState m_idleState = new IdleState();
    WalkState m_walkState = new WalkState();
    AvoidState m_avoidState = new AvoidState();
    AttackState m_attackState = new AttackState();
    RunState m_runState = new RunState();
    JumpState m_jumpState = new JumpState();
    FallState m_fallState = new FallState();
    LandState m_landState = new LandState();
    #endregion




    #region Attack
    /// <summary>アニメーションが再生中かどうか</summary>
    bool m_isAnimationPlaying = false;
    /// <summary>次の攻撃までの入力受付時間</summary>
    float m_waitTimer = 0.0f;
    /// <summary>攻撃のフラグ</summary> 
    bool m_reserveAction = false;
    /// <summary>コンボ数</summary> 
    int m_comboStep = 0;
    /// <summary>アクションの持続時間</summary>
    float m_actionKeepingTimer = 0.0f;
    bool m_poseKeep = false;

    List<Attack> m_currentAttackList = new List<Attack>();
    List<Attack> m_currentAirialAttackList = new List<Attack>();
    List<Attack> m_currentSkillList = new List<Attack>();
    [SerializeField]
    WeaponType m_weaponType = WeaponType.HEAVY_SWORD;
    #endregion

    /// <summary>
    /// 空中にいるかどうかのフラグ
    /// </summary>
    bool m_inKeepAir = default;

    [SerializeField]
    float m_airDasuSpeed = 1.0f;

    [SerializeField]
    float m_airDasuTime = 0.5f;

    [SerializeField]
    float m_airKeepTime = 1.0f;

    float m_airKeepTimer;

    bool m_airAttackEnd = default;

    [SerializeField]
    int m_jumpStep = 2;
    int m_currentJumpStep = default;

    [SerializeField]
    int m_airDushCount = 1;
    int m_currentAirDushCount = default;
    bool m_stateKeep;

    List<EnemyBase> m_targetEnemys = new List<EnemyBase>();

    Vector3 m_moveForward = Vector3.zero;
    Vector3 m_currentVelocity = Vector3.zero;
    Vector3 m_inputDir = Vector3.zero;
    Quaternion m_targetRot = Quaternion.identity;
    void Start()
    {
        m_anim = GetComponentInChildren<Animator>();
        m_controller = GetComponent<CharacterController>();
        m_attackAssist = GetComponent<AttackAssistController>();
        m_weaponHolder = GetComponentInChildren<WeaponHolder>();
        m_animCtrl.SetEventDelegate(EventCall);
        ChangeState(m_idleState);
        m_inputManager = InputManager.Instance;

        m_selfTrans = transform;
        m_currentJustTime = m_justTime;
        m_currentGravityScale = m_gravityScale;
        m_currentJumpStep = m_jumpStep;
    }

    void Update()
    {
        ApplyAxis();
        ApplyMove();
        ApplyGravity();
        ApplyRotation();
        CheckAir();

        m_currentState.OnUpdate(this);
    }

    void ApplyAxis()
    {
        m_moveForward = Camera.main.transform.TransformDirection(m_inputDir);
        m_moveForward.y = 0.0f;
        m_moveForward.Normalize();
        m_inputDir = m_inputManager.InputDir;
    }

    void ApplyMove()
    {
        var velocity = Vector3.Scale(m_currentVelocity, new Vector3(m_moveSpeed, 1f, m_moveSpeed));
        m_controller.Move(Time.deltaTime * velocity);
    }

    void ApplyRotation()
    {
        var rot = m_selfTrans.rotation;
        rot = Quaternion.Slerp(rot, m_targetRot, m_currentRotateSpeed * Time.deltaTime);
        m_selfTrans.rotation = rot;
    }

    void LookAt(Vector3 target)
    {
        var targetRot = Quaternion.LookRotation(target);
        var rot = m_selfTrans.rotation;
        rot = Quaternion.Slerp(rot,targetRot, 1000.0f);
        m_selfTrans.rotation = rot;
    }

    void MoveForward(float time, float speed)
    {
        //StartCoroutine(MoveForwardAsync(time,speed));
    }

    //IEnumerator MoveForwardAsync(float time, float speed)
    //{
    //    while (time < 0.0f)
    //    {
    //        time -= Time.deltaTime;
    //        m_currentVelocity += transform.forward * speed * Time.deltaTime;
    //        yield return null;
    //    }
    //}

    void CheckAir()
    {
        if (m_inKeepAir)
        {
            m_airKeepTimer -= Time.deltaTime;
        }

        if (m_airKeepTimer < 0.0)
        {
            m_airKeepTimer = m_airKeepTime;
            m_inKeepAir = false;
        }
    }
    bool IsGround()
    {
        Vector3 start = new Vector3(transform.position.x, transform.position.y + 0.7f, transform.position.z);
        Ray ray = new Ray(start, Vector3.down);
        bool isGround = Physics.SphereCast(ray, m_isGroundRadius, m_isGroundLength, m_groundLayer);
        return isGround;
    }

    private void OnDrawGizmos()
    {
        Vector3 start = new Vector3(transform.position.x, transform.position.y + 0.7f, transform.position.z);
        Vector3 end = start + Vector3.down * m_isGroundLength;
        Color color = new Color(1, 0, 0);
        Gizmos.DrawWireSphere(end, m_isGroundRadius);
    }

    void ApplyGravity()
    {
        if (!(IsGround() || m_controller.isGrounded) && !m_inKeepAir)
        {
            m_currentVelocity.y += m_currentGravityScale * Physics.gravity.y * Time.deltaTime;
        }
        else if (m_inKeepAir)
        {
            m_currentVelocity.y = 0f;
        }
    }

    void PlayAnimation(string stateName, float transitionTime = 0.2f, int layer = 0)
    {
        m_anim.CrossFadeInFixedTime(stateName, transitionTime, layer);
    }

    void ChangeState(PlayerStateBase nextState)
    {
        m_currentState?.OnExit(this, nextState);
        nextState?.OnEnter(this, m_currentState);
        m_currentState = nextState;
    }

    public void AddDamage(int damage)
    {
        if (m_justTrigger)
        {
            justsubject.OnNext(Unit.Default);
        }
        else
        {
            m_hp.Value -= damage;
            if (m_hp.Value <= 0)
            {
                playerDeath.OnNext(Unit.Default);
            }
        }
    }

    void AttackAssist()
    {
        if (m_attackAssist.Target != null)
        {
            transform.LookAt(m_attackAssist.Target.transform);
        }
        var dir = m_selfTrans.forward;
        dir.y = 0.0f;
        m_targetRot = Quaternion.LookRotation(dir);
    }

    void NextAction(int step, AttackLayer layer, List<Attack> comboList)
    {

        int actId = -1;
        Attack attack = comboList[0];
        for (int i = 0; i < comboList.Count; i++)
        {
            if (comboList[i].Step != step) continue;
            if (comboList[i].Layer != layer) continue;

            attack = comboList[i];
            actId = i;
            break;
        }
        if (actId == -1)
        {
            Debug.LogWarning(string.Format("attack not found. {0}/1", step, layer));
            return;
        }

        switch (attack.ActType)
        {
            case ActionType.Animation:
                AttackAssist();
                Debug.Log($"{step}/{layer}{comboList[actId].Name}");
                m_hitCtrl.SetCtrl(this, comboList[actId]);
                m_isAnimationPlaying = true;
                m_waitTimer = attack.WaitTime;
                m_actionKeepingTimer = attack.KeepTime;
                m_animCtrl.Play(attack.ActionTargetName, 0.2f);
                m_animCtrl.SetPlayBackDelegate((int targetLayer) =>
                {
                    m_isAnimationPlaying = false;
                });
                break;
            case ActionType.CreateObject:
                Debug.Log("CreateObject");
                break;
            default:
                break;
        }

        switch (attack.Layer)
        {
            case AttackLayer.InFight:
                break;
            case AttackLayer.LongRange:
                break;
            case AttackLayer.Airial:
                //var velo = transform.forward * 0.3f;
                Debug.Log("AirialAttack");
                m_inKeepAir = true;
                m_airKeepTimer = 0.7f;
                MoveForward(0.2f, 1.0f);
                //m_currentVelocity = transform.forward * 0.7f;
                //m_controller.Move(m_currentVelocity);

                break;
            default:
                break;
        }
    }

    void EventCall(int eventId)
    {
        switch (eventId)
        {
            case 1:

                Debug.Log("打ち上げ");
                break;
        }
    }

    public void HitCallBack(EnemyBase enemy, Attack attack)
    {
        //enemys?.AddDamage(attack.Damage);
        enemy?.AddDamage(attack.Damage);
        m_actionCtrl.HitStop(attack.Power);

        if (!m_targetEnemys.Contains(enemy))
        {
            m_targetEnemys.Add(enemy);
        }
        //comboSubject.OnNext(Unit.Default);
    }
    public void StartAttack()
    {
        //m_atkTrigeer.enabled = true;
        m_weaponHolder.WeaponTriggerEnable();
    }

    public void AttackEnd()
    {
        //m_atkTrigeer.enabled = false;
        m_weaponHolder.WeaponTriggerDisable();
    }
}
