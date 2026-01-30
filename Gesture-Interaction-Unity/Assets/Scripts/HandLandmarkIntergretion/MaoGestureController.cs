using UnityEngine;
using System.Collections.Generic;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class MaoGestureController : MonoBehaviour
{
    [Header("Animation Settings")]
    private Animator animator;
    public string motionStateName = "mtn_01"; 

    [Header("Wave Detection Settings")]
    public float waveThreshold = 0.1f;   
    public float waveTimeWindow = 1.0f;   
    public int requiredSwings = 3;        

    private List<float> xPositions = new List<float>();
    private float timer = 0f;
    private bool isWaving = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Mao 오브젝트에 Animator가 없습니다!");
        }
    }

    void Update()
    {
        if (HandLandmarkDataManager.Instance == null) return;

        
        bool detected = HandLandmarkDataManager.Instance.IsRightHandDetected ||
                        HandLandmarkDataManager.Instance.IsLeftHandDetected;

        if (detected)
        {
            
            Vector2 handPos = HandLandmarkDataManager.Instance.IsRightHandDetected ?
                HandLandmarkDataManager.Instance.RightHandPosition :
                HandLandmarkDataManager.Instance.LeftHandPosition;

            DetectWave(handPos.x);
        }
        else
        {
            ClearData();
        }
    }

    private void DetectWave(float currentX)
    {
        timer += Time.deltaTime;
        xPositions.Add(currentX);

        
        if (timer > waveTimeWindow)
        {
            AnalyzeMovement();
            ClearData();
        }
    }

    private void AnalyzeMovement()
    {
        if (xPositions.Count < 10) return;

        int swings = 0;
        float lastPos = xPositions[0];
        bool movingRight = xPositions[1] > xPositions[0];

        for (int i = 1; i < xPositions.Count; i++)
        {
            float diff = xPositions[i] - lastPos;

            
            if (movingRight && diff < -waveThreshold)
            {
                swings++;
                movingRight = false;
                lastPos = xPositions[i];
            }
            else if (!movingRight && diff > waveThreshold)
            {
                swings++;
                movingRight = true;
                lastPos = xPositions[i];
            }
        }

       
        if (swings >= requiredSwings && !isWaving)
        {
            TriggerMaoMotion();
        }
    }

    private void TriggerMaoMotion()
    {
        Debug.Log("손 흔들기 감지! Mao가 mtn_01 동작을 시작합니다.");
        if (animator != null)
        {
           
            animator.Play(motionStateName, 0, 0f);
        }

      
        StartCoroutine(WaveCooldown());
    }

    private System.Collections.IEnumerator WaveCooldown()
    {
        isWaving = true;
        yield return new WaitForSeconds(2.5f); 
        isWaving = false;
    }

    private void ClearData()
    {
        xPositions.Clear();
        timer = 0f;
    }
}
