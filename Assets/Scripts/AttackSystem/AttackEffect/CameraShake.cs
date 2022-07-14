using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : IAttackEffect
{
    [Header("カメらを揺らす方向")]
    public Vector3 CameraShakeVec;
    public void SetEffect()
    {
        CameraManager.ShakeCam(CameraShakeVec);
    }

    public void SetUp(GameObject owner)
    {

    }
}