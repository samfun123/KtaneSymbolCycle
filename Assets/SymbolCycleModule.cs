﻿using System;
using System.Collections;
using System.Linq;
using SymbolCycle;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Symbol Cycles
/// Created by Timwi
/// </summary>
public class SymbolCycleModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public MeshRenderer[] ScreenSymbols;
    public TextMesh NumberDisplay;
    public Texture[] Symbols;
    public Transform Switch;

    public KMSelectable SwitchSelectable;
    public KMSelectable[] ScreenSelectables;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private enum State
    {
        Cycling,
        Retrotransphasic,
        Anterodiametric,
        Solved
    }

    private int[][] _cycles;
    private int[][] _selectableSymbols;
    private int[] _selectedSymbolIxs;
    private int _cycleNumber;
    private State _state;
    private int[] _offsets;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        Array.Sort(Symbols, (a, b) => int.Parse(a.name.Substring(4)).CompareTo(int.Parse(b.name.Substring(4))));
        SwitchSelectable.OnInteract = toggleSwitch;
        for (int i = 0; i < 2; i++)
            ScreenSelectables[i].OnInteract = getScreenClickHandler(i);

        ResetModule();
    }

    private void ResetModule()
    {
        var allSymbols = Enumerable.Range(0, Symbols.Length).ToList().Shuffle();
        var cycleLength1 = new[] { 2, 3, 4, 5 }[Rnd.Range(0, 4)];
        var cycleLengths2 = (cycleLength1 == 2 || cycleLength1 == 4 ? new[] { 3, 5 } : new[] { 2, 3, 4, 5 }.Except(new[] { cycleLength1 })).ToArray();
        var cycleLength2 = cycleLengths2[Rnd.Range(0, cycleLengths2.Length)];

        _cycles = new[] {
            allSymbols.Take(cycleLength1).ToArray(),
            allSymbols.Skip(cycleLength1).Take(cycleLength2).ToArray()
        };

        _state = State.Cycling;
        _cycleNumber = Rnd.Range(10, 100);
        StartCoroutine(CycleSymbols());

        Debug.LogFormat("[Symbol Cycle #{0}] Left cycle: [{1}]", _moduleId, _cycles[0].JoinString(", "));
        Debug.LogFormat("[Symbol Cycle #{0}] Right cycle: [{1}]", _moduleId, _cycles[1].JoinString(", "));
    }

    private KMSelectable.OnInteractHandler getScreenClickHandler(int i)
    {
        ScreenSelectables[i].AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ScreenSelectables[i].transform);

        return delegate
        {
            switch (_state)
            {
                case State.Cycling:
                    Module.HandleStrike();
                    break;

                case State.Retrotransphasic:
                    _selectedSymbolIxs[i] = (_selectedSymbolIxs[i] + 1) % _selectableSymbols[i].Length;
                    ScreenSymbols[i].material.mainTexture = Symbols[_selectableSymbols[i][_selectedSymbolIxs[i]]];
                    break;

                case State.Anterodiametric:
                    _cycleNumber += _offsets[i];
                    NumberDisplay.text = _cycleNumber.ToString();
                    break;

                case State.Solved:
                    break;
            }

            return false;
        };
    }

    private bool toggleSwitch()
    {
        SwitchSelectable.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, SwitchSelectable.transform);

        switch (_state)
        {
            case State.Cycling:
                StartCoroutine(toggleSwitch(0, 30));
                _state = Rnd.Range(0, 2) == 0 ? State.Retrotransphasic : State.Anterodiametric;
                _cycleNumber = Rnd.Range(1000000, 100000000);
                NumberDisplay.text = _cycleNumber.ToString();

                _offsets = new[] { -1, 1 }.Shuffle();
                _selectableSymbols = new int[2][];
                var decoys = Enumerable.Range(0, Symbols.Length).Except(_cycles.SelectMany(x => x)).ToList().Shuffle();
                for (int i = 0; i < 2; i++)
                {
                    var numDecoys = Rnd.Range(1, 4);
                    _selectableSymbols[i] = _cycles[i].Concat(decoys.Take(numDecoys)).ToArray().Shuffle();
                    decoys.RemoveRange(0, numDecoys);
                }

                Debug.LogFormat("[Symbol Cycle #{0}] Switching to {1} state.", _moduleId, _state);
                Debug.LogFormat("[Symbol Cycle #{0}] Displayed cycle number is: {1}", _moduleId, _cycleNumber);

                _selectedSymbolIxs = new int[2];
                var anterodiametricCycleNumber = _cycleNumber + Rnd.Range(0, 60);
                for (int i = 0; i < 2; i++)
                {
                    // Make sure that we show a valid symbol combination in case it’s the anterodiametric state
                    _selectedSymbolIxs[i] = Array.IndexOf(_selectableSymbols[i], _cycles[i][anterodiametricCycleNumber % _cycles[i].Length]);
                    ScreenSymbols[i].material.mainTexture = Symbols[_selectableSymbols[i][_selectedSymbolIxs[i]]];
                }
                if (_state == State.Retrotransphasic)
                    Debug.LogFormat("[Symbol Cycle #{0}] Solution: {1}", _moduleId, Enumerable.Range(0, 2).Select(i => _cycles[i][_cycleNumber % _cycles[i].Length]).JoinString(", "));
                else
                {
                    Debug.LogFormat("[Symbol Cycle #{0}] Displayed symbols: {1}", _moduleId, Enumerable.Range(0, 2).Select(i => _selectableSymbols[i][_selectedSymbolIxs[i]]).JoinString(", "));
                    Debug.LogFormat("[Symbol Cycle #{0}] Possible solution: {1}", _moduleId, anterodiametricCycleNumber);
                }
                break;

            case State.Retrotransphasic:
            case State.Anterodiametric:
                StartCoroutine(toggleSwitch(30, 0));
                var correct = true;
                for (int i = 0; i < 2; i++)
                    if (_selectableSymbols[i][_selectedSymbolIxs[i]] != _cycles[i][_cycleNumber % _cycles[i].Length])
                        correct = false;

                if (correct)
                {
                    Debug.LogFormat("[Symbol Cycle #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    for (int i = 0; i < 2; i++)
                        ScreenSymbols[i].gameObject.SetActive(false);
                    _state = State.Solved;
                }
                else
                {
                    if (_state == State.Anterodiametric)
                        Debug.LogFormat("[Symbol Cycle #{0}] Wrong solution entered: {1}", _moduleId, _cycleNumber);
                    else
                        Debug.LogFormat("[Symbol Cycle #{0}] Wrong solution entered: {1}", _moduleId, Enumerable.Range(0, 2).Select(i => _selectableSymbols[i][_selectedSymbolIxs[i]]).JoinString(", "));
                    Module.HandleStrike();
                    ResetModule();
                }
                break;

            case State.Solved:
                break;
        }

        return false;
    }

    private bool _togglingSwitch = false;
    private IEnumerator toggleSwitch(float from, float to)
    {
        while (_togglingSwitch)
            yield return null;
        _togglingSwitch = true;

        var cur = from;
        var stop = false;
        while (!stop)
        {
            cur += 250 * Time.deltaTime * (to > from ? 1 : -1);
            if ((to > from && cur >= to) || (to < from && cur <= to))
            {
                cur = to;
                stop = true;
            }
            Switch.localEulerAngles = new Vector3(0, 90, 180 + cur);
            yield return null;
        }
        _togglingSwitch = false;
    }

    private IEnumerator CycleSymbols()
    {
        while (_state == State.Cycling)
        {
            _cycleNumber++;
            NumberDisplay.text = _cycleNumber.ToString();
            for (int i = 0; i < 2; i++)
                ScreenSymbols[i].material.mainTexture = Symbols[_cycles[i][_cycleNumber % _cycles[i].Length]];
            var time = Time.time;
            yield return new WaitUntil(() => Time.time - time >= 1.47f || _state != State.Cycling);
        }
    }

	private bool EqualsAny(object obj, params object[] targets)
	{
		return targets.Contains(obj);
	}

	public string TwitchHelpText = "Flip the switch by doing !{0} flip. The module will cycle through the screens automatically, but you can do it again using !{0} cycle. Click a screen a certain number of times by doing !{0} click 1 3, which will click the left screen 3 times.";
	public IEnumerator ProcessTwitchCommand(string command)
	{
		string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		
		if (EqualsAny(split[0], "click", "press") && split.Length == 3 && _state != State.Cycling)
		{
			yield return null;
			yield return "trycancel"; // Doesn't really matter where this command is cancelled.

			int screen;
			int clicks;
			
			if (int.TryParse(split[1], out screen) && int.TryParse(split[2], out clicks))
			{
				KMSelectable screenSelectable = ScreenSelectables[--screen];
				if (!screenSelectable) yield break;

				float clickSpeed = Math.Min(1.5f / clicks, 0.1f);
				
				for (int i = 0; i < clicks; i++)
				{
					screenSelectable.OnInteract();

					if (clickSpeed > 0.001f)
					{
						yield return new WaitForSeconds(clickSpeed);
					}
				}
			}
		}
		else if (EqualsAny(split[0], "flip", "switch") && split.Length == 1)
		{
			yield return null;

			bool wasCycling = _state == State.Cycling;

			SwitchSelectable.OnInteract();
			yield return new WaitForSeconds(0.1f);

			if (wasCycling)
			{
				IEnumerator enumator = ProcessTwitchCommand("cycle");
				while (enumator.MoveNext()) yield return enumator.Current;
			}
		}
		else if (split[0] == "cycle" && split.Length == 1 && _state != State.Cycling)
		{
			yield return null;

			switch (_state)
			{
				case State.Retrotransphasic:
					for (int s = 0; s < ScreenSelectables.Length; s++)
					{
						for (int i = 0; i < _selectableSymbols[s].Length; i++)
						{
							yield return new WaitForSeconds(1f);
							ScreenSelectables[s].OnInteract();
						}
					}

					break;
				case State.Anterodiametric:
					for (int s = 0; s < ScreenSelectables.Length; s++)
					{
						yield return new WaitForSeconds(1f);
						ScreenSelectables[s].OnInteract();
					}

					break;
			}
		}
	}
}
