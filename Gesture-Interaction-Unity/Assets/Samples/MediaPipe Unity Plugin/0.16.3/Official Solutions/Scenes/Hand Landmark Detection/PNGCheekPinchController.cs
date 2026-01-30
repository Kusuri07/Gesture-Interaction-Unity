using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

/// <summary>
/// HandLandmarkerResult 타입 구조 확인용 디버그 스크립트
/// </summary>
public class MediaPipeTypeDebugger : MonoBehaviour
{
    public void ProcessHandLandmarks(HandLandmarkerResult result)
    {
        Debug.Log("=== HandLandmarkerResult Type Debug ===");

        // handLandmarks 타입 확인
        if (result.handLandmarks != null)
        {
            Debug.Log($"handLandmarks type: {result.handLandmarks.GetType().Name}");
            Debug.Log($"handLandmarks count: {result.handLandmarks.Count}");

            if (result.handLandmarks.Count > 0)
            {
                var first = result.handLandmarks[0];
                Debug.Log($"handLandmarks[0] type: {first.GetType().Name}");

                // 모든 public 프로퍼티 출력
                var props = first.GetType().GetProperties();
                Debug.Log($"Properties of {first.GetType().Name}:");
                foreach (var prop in props)
                {
                    Debug.Log($"  - {prop.Name}: {prop.PropertyType.Name}");
                }
            }
        }

        // handedness 타입 확인
        if (result.handedness != null)
        {
            Debug.Log($"handedness type: {result.handedness.GetType().Name}");
            Debug.Log($"handedness count: {result.handedness.Count}");

            if (result.handedness.Count > 0)
            {
                var first = result.handedness[0];
                Debug.Log($"handedness[0] type: {first.GetType().Name}");

                // 모든 public 프로퍼티 출력
                var props = first.GetType().GetProperties();
                Debug.Log($"Properties of {first.GetType().Name}:");
                foreach (var prop in props)
                {
                    Debug.Log($"  - {prop.Name}: {prop.PropertyType.Name}");
                }
            }
        }
    }
}