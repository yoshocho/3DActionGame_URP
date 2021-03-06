using AttackSetting;
using UnityEngine;
using State = StateMachine<NormalStateEnemy>.State;
using ObjectPool;
using System;

[RequireComponent(typeof(GroundChecker), typeof(ActionCtrl))]
public partial class NormalStateEnemy : EnemyBase
{
    enum StateType
    {
        Idle,
        Chase,
        Attack,
        Damage,
    }
    enum Attack
    {
        Single,
        Combo,
    }
    [SerializeField, Range(0.5f, 5.0f)]
    float _attackRange = 1.5f;
    [SerializeField, Range(2.0f, 20f)]
    float _waitTime = 2.0f;
    [SerializeField]
    float _gravityScale = 0.98f;
    [SerializeField]
    float _rotateSpeed;


    Transform _selfTrans;
    float _distance;
    Quaternion _targetRot;
    Vector3 _currentVelocity;

    Attack _attackType = Attack.Single;

    StateMachine<NormalStateEnemy> _stateMachine;
    ActionCtrl _actCtrl;
    GroundChecker _groundChecker;
    AnimationCtrl _animCtrl;

    [SerializeField]
    bool _debagMode;

    protected override void SetUp()
    {
        base.SetUp();
        Init();
        StateCash();
    }
    void Init()
    {
        _selfTrans = transform;
        _actCtrl = GetComponent<ActionCtrl>();
        _groundChecker = GetComponent<GroundChecker>();
        if (!_animCtrl) _animCtrl = GetComponentInChildren<AnimationCtrl>();
    }

    private void StateCash()
    {
        _stateMachine = new StateMachine<NormalStateEnemy>(this);

        _stateMachine
            .AddAnyTransition<IdleState>((int)StateType.Idle)
            .AddAnyTransition<RunState>((int)StateType.Chase)
            .AddAnyTransition<AttackState>((int)StateType.Attack)
            .AddAnyTransition<DamageState>((int)StateType.Damage)
            .Start<IdleState>();
    }

    private void Update()
    {
        _stateMachine.Update();
        _distance = Vector3.Distance(transform.position, _targetTrans.position);
    }
    private void FixedUpdate()
    {
        ApplyMove();
        ApplyRotate();
        ApplyGravity();
    }

    void ApplyMove()
    {
        var velo = Vector3.Scale(_currentVelocity, new Vector3(MoveSpeed, 1.0f, MoveSpeed));
        RB.velocity = velo;
    }
    void ApplyRotate()
    {
        var rot = _selfTrans.rotation;
        rot = Quaternion.Lerp(rot, _targetRot, _rotateSpeed * Time.deltaTime);
        _selfTrans.rotation = rot;
    }

    void ApplyGravity()
    {
        if(!_groundChecker.IsGround())
        _currentVelocity.y += _gravityScale * Physics.gravity.y * Time.deltaTime;
    }

    void PlayAnim(string stateName,float dur = 0.1f,int layer = 0,Action onAnimEnd = null)
    {
        _animCtrl.Play(stateName, dur,layer,onAnimEnd);
    }
    void ChangeState(StateType state)
    {
        _stateMachine.Dispatch((int)state);
    }

    public override void AddDamage(int damage, AttackType attackType = AttackType.Weak)
    {

        base.AddDamage(damage, attackType);

        if (_debagMode) Debug.Log(Status.CurrentHp);
        if (IsDeath)
        {

        }
        else ChangeState(StateType.Damage);
    }
    class RunState : State
    {
        Vector3 _axis = Vector3.zero;
        protected override void OnEnter(State prevState)
        {
            if (owner._debagMode) Debug.Log("InChase");
            owner.PlayAnim("Run", 0.2f);
        }
        protected override void OnUpdate()
        {
            _axis = (owner._targetTrans.position - owner._selfTrans.position).normalized;
            _axis.y = 0.0f;
            owner._targetRot = Quaternion.LookRotation(_axis);
            owner._currentVelocity = new Vector3(_axis.x, owner._currentVelocity.y, _axis.z);
            if (owner._groundChecker.IsGround())
            {
                if (owner._distance < owner._attackRange) owner.ChangeState(StateType.Attack);
            }
        }
        protected override void OnExit(State nextState)
        {

        }
    }
    class IdleState : State
    {
        protected override void OnEnter(State prevState)
        {
            if (owner._debagMode) Debug.Log("InIdle");
            owner._currentVelocity.x = 0.0f;
            owner._currentVelocity.y = 0.0f;
            owner.PlayAnim("Idle");
        }
        protected override void OnUpdate()
        {
            if (owner._groundChecker.IsGround()) 
            {
                if (owner._distance > owner._attackRange) owner.ChangeState(StateType.Chase);
            }
        }
        protected override void OnExit(State nextState)
        {

        }
    }
    class AttackState : State
    {
        float _waitTimer = 0.0f;
        protected override void OnEnter(State prevState)
        {
            if (owner._debagMode) Debug.Log("InAttack");

            owner._currentVelocity.x = 0.0f;
            owner._currentVelocity.z = 0.0f;
            //_waitTimer = owner._waitTime;
            owner._actCtrl.RequestAction(AttackType.Weak);
        }
        protected override void OnUpdate()
        {
            _waitTimer += Time.deltaTime;

            if (owner._distance < owner._attackRange)
            {
                switch (owner._attackType)
                {
                    case Attack.Single:
                        if (_waitTimer > owner._waitTime)
                        {
                            owner._actCtrl.RequestAction(AttackType.Weak);
                            _waitTimer = 0.0f;
                        }
                        break;
                    case Attack.Combo:
                        break;
                    default:
                        break;
                }
            }
            else if (!owner._actCtrl.ActionKeep)
            {
                if (owner._distance > owner._attackRange) owner.ChangeState(StateType.Chase);
            }
        }
        protected override void OnExit(State nextState)
        {
            _waitTimer = 0.0f;
        }
    }
    class DamageState : State
    {
        protected override void OnEnter(State prevState)
        {
            if (owner._debagMode) Debug.Log("InDamage");
            base.OnEnter(prevState);
        }
    }
}
