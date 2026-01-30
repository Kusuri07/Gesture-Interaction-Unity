// GestureTypes.cs
// 제스처 관련 공통 타입 정의
// 여러 스크립트에서 공유하여 중복 정의 방지

using UnityEngine;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    /// <summary>
    /// 손 제스처 타입 정의
    /// </summary>
    public enum GestureType
    {
        None,           // 제스처 없음
        Fist,           // 주먹
        OpenHand,       // 펼친 손
        FingerGun,      // 손가락 총
        Peace,          // 브이 (피스)
        ThumbsUp,       // 엄지 척
        Pointing,       // 검지로 가리키기
        RockSign,       // 락 사인 (뿔)
        OKSign,         // OK 사인
        Pinch           // 핀치 (터치용)
    }

    /// <summary>
    /// 제스처-애니메이션 매핑 정의
    /// </summary>
    [System.Serializable]
    public class GestureAnimationMapping
    {
        public GestureType gesture;
        [Tooltip("Animator의 트리거 이름")]
        public string animationTrigger;
    }

    /// <summary>
    /// 터치 가능 영역 정의
    /// </summary>
    [System.Serializable]
    public class TouchZone
    {
        public string zoneName; // "Head", "Cheek", "Body" 등
        [Range(0f, 1f)] public float minX;
        [Range(0f, 1f)] public float maxX;
        [Range(0f, 1f)] public float minY;
        [Range(0f, 1f)] public float maxY;
        public string animationTrigger;
    }
}
