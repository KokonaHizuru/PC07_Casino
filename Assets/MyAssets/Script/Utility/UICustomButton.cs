using UnityEngine;
using UnityEngine.UI;

public class UICustomButton : MonoBehaviour
{
	Button button;

	[SerializeField]
	AudioClip clickAudio;

	[SerializeField]
	AudioClip mouseoverAudio;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		button = GetComponent<Button>();

	}

    // Update is called once per frame
    void Update()
    {
        
    }

	/// <summary>
	/// クリック時の処理
	/// </summary>
	public void OnCkick()
	{
		if (button.interactable)
			AudioManager.Instance.systemSeManager.PlayOneShot(clickAudio);
	}

	/// <summary>
	/// マウスが入ったときの処理
	/// </summary>
	public void OnMouseEnter()
	{
		AudioManager.Instance.systemSeManager.PlayOneShot(mouseoverAudio);
	}
}
