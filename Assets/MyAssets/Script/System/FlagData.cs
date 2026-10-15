using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FlagData
{
	public Dictionary<string, bool> boolFlag = new Dictionary<string, bool>();
	public Dictionary<string, int> intFlag = new Dictionary<string, int>();
	public Dictionary<string, float> floatFlag = new Dictionary<string, float>();

	public void Clear()
	{
		boolFlag.Clear();
		intFlag.Clear();
		floatFlag.Clear();
	}

	public void SetFlag(string key, bool value)
	{
		boolFlag[key] = value;
	}
	public bool Is(string key, bool value)
	{
		if (!boolFlag.ContainsKey(key))
		{
			return false == value;
		}

		return boolFlag[key] == value;
	}


	public void SetFlag(string key, int value)
	{
		intFlag[key] = value;
	}
	public bool Is(string key, int value)
	{
		if (!intFlag.ContainsKey(key))
			return false;

		return intFlag[key] == value;
	}


	public void SetFlag(string key, float value)
	{
		floatFlag[key] = value;
	}
	public bool Is(string key, float value)
	{
		if (!floatFlag.ContainsKey(key))
			return false;

		return floatFlag[key] == value;
	}
}
