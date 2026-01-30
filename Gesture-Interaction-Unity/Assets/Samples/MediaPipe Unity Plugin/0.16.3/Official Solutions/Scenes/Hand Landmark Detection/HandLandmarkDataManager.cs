using System;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public class HandLandmarkDataManager : MonoBehaviour
    {
        private static HandLandmarkDataManager _instance;
        public static HandLandmarkDataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<HandLandmarkDataManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("HandLandmarkDataManager");
                        _instance = go.AddComponent<HandLandmarkDataManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        public int HandCount { get; private set; }

        public List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> LeftHandLandmarks { get; private set; }
        public List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> RightHandLandmarks { get; private set; }

        public bool IsLeftHandDetected => LeftHandLandmarks != null && LeftHandLandmarks.Count > 0;
        public bool IsRightHandDetected => RightHandLandmarks != null && RightHandLandmarks.Count > 0;

        public Vector2 LeftHandPosition { get; private set; }
        public Vector2 RightHandPosition { get; private set; }

        public event Action<HandLandmarkerResult> OnHandLandmarksUpdated;

        [Header("Performance Settings")]
        public int updateInterval = 1;
        private int frameCount = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LeftHandLandmarks = new List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark>();
            RightHandLandmarks = new List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark>();
        }

        public void UpdateHandLandmarks(HandLandmarkerResult result)
        {
            if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            {
                ClearHandData();
                return;
            }

            frameCount++;
            if (frameCount % updateInterval != 0) return;

            HandCount = result.handLandmarks.Count;
            LeftHandLandmarks.Clear();
            RightHandLandmarks.Clear();

            for (int i = 0; i < HandCount; i++)
            {
                var landmarksContainer = result.handLandmarks[i];
                if (landmarksContainer.landmarks == null) continue;
                var actualLandmarks = landmarksContainer.landmarks;

                if (result.handedness == null || i >= result.handedness.Count) continue;
                var categories = result.handedness[i].categories;
                if (categories == null || categories.Count == 0) continue;

                string label = categories[0].categoryName;
                bool isLeftHand = label.ToLower().Contains("left");

                if (actualLandmarks.Count > 0)
                {
                    var wrist = actualLandmarks[0];
                    Vector2 wristPos = new Vector2(wrist.x, wrist.y);

                    if (isLeftHand)
                    {
                        LeftHandLandmarks.AddRange(actualLandmarks);
                        LeftHandPosition = wristPos;
                    }
                    else
                    {
                        RightHandLandmarks.AddRange(actualLandmarks);
                        RightHandPosition = wristPos;
                    }
                }
            }

            OnHandLandmarksUpdated?.Invoke(result);
        }

        private void ClearHandData()
        {
            HandCount = 0;
            LeftHandLandmarks.Clear();
            RightHandLandmarks.Clear();
            LeftHandPosition = Vector2.zero;
            RightHandPosition = Vector2.zero;
        }

        public Vector3 GetLandmarkPosition(bool isLeftHand, int landmarkIndex)
        {
            var landmarks = isLeftHand ? LeftHandLandmarks : RightHandLandmarks;
            if (landmarks == null || landmarks.Count <= landmarkIndex || landmarkIndex < 0) return Vector3.zero;

            var landmark = landmarks[landmarkIndex];
            return new Vector3(landmark.x, landmark.y, landmark.z);
        }

        public Vector3 GetIndexFingerTip(bool isLeftHand) => GetLandmarkPosition(isLeftHand, 8);
        public Vector3 GetThumbTip(bool isLeftHand) => GetLandmarkPosition(isLeftHand, 4);
        public Vector3 GetWristPosition(bool isLeftHand) => GetLandmarkPosition(isLeftHand, 0);
    }
}
