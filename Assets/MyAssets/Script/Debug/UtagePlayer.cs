using System.Collections;
using UnityEngine;

public class UtagePlayer : MonoBehaviour
{
	[SerializeField]
	string scenarioLabel;

	[SerializeField]
	int emotionLevel;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	IEnumerator Start()
    {
		yield return new WaitForSeconds(1.0f);

		// 宴に感情レベルを設定
		AdvEngineController.Instance.SetParameter("Emotion", emotionLevel);

		AdvEngineController.Instance.JumpScenario(
			scenarioLabel,
			() =>
			{
			}
		);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
