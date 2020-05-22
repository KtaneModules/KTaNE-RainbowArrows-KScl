using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

using RNG = UnityEngine.Random;

public class RainbowArrows : MonoBehaviour
{
	// Standardized logging
	private static int globalLogID = 0;
	private int thisLogID;
	private bool moduleSolved;

	public KMBombInfo bombInfo;
	public KMAudio bombAudio;
	public KMBombModule bombModule;

	public Material[] colorMeshes; // ROYGBIVW
	public KMSelectable[] arrowButtons; // In order, clockwise from north
	public TextMesh display;

	private readonly string[] __positionText = new string[] {
		"north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"
	};

	// -----
	// Animations
	// -----

	private float startColor;
	private Coroutine currentCoroutine;

	IEnumerator SolveAnimation()
	{
		int i = 0;
		yield return null;

		// Shortened solve animation for extremely low amounts of time, because I'm generous
		if (bombInfo.GetTime() < 7f)
		{
			for (;i < 10; ++i)
			{
				display.text = RNG.Range(0,10).ToString("\\G0");
				display.color = Color.HSVToRGB(startColor, (10 - i) * 0.1f, 1.0f);
				yield return new WaitForSeconds(0.025f);
			}
		}
		else
		{
			for (;i < 50; ++i)
			{
				display.text = RNG.Range(0,100).ToString("00");
				display.color = Color.HSVToRGB((startColor + (i * 0.01f)) % 1.0f, 1.0f, 1.0f);
				yield return new WaitForSeconds(0.025f);
			}
			for (;i < 80; ++i)
			{
				display.text = RNG.Range(0,10).ToString("\\G0");
				display.color = Color.HSVToRGB((startColor + (i * 0.01f)) % 1.0f, 1.0f, 1.0f);
				yield return new WaitForSeconds(0.025f);
			}
			for (;i < 100; ++i)
			{
				display.text = RNG.Range(0,10).ToString("\\G0");
				display.color = Color.HSVToRGB((startColor + (i * 0.01f)) % 1.0f, (100 - i) * 0.05f, 1.0f);
				yield return new WaitForSeconds(0.025f);
			}
		}
		display.text = "GG";
		display.color = new Color(1.0f, 1.0f, 1.0f);

		bombModule.HandlePass();
		currentCoroutine = null;
	}

	IEnumerator StartupAnimation()
	{
		display.text = "";
		yield return new WaitForSeconds(0.5f);

		display.text = (displayedDigits / 10).ToString("0");
		yield return new WaitForSeconds(0.5f);

		display.text = displayedDigits.ToString("00");
	}


	// -----
	// Module answer generation
	// -----

	private readonly int[] __uniquenessOrderCW  = new int[] {0, 1, 7, 2, 6, 3, 5, 4};
	private readonly int[] __uniquenessOrderCCW = new int[] {0, 7, 1, 6, 2, 5, 3, 4};

	// Arrows' state; only set once and never changed, even on reset
	private int whiteLocation = 0;
	private bool ccwRainbow;

	// Digits and solution, changes on reset
	private int displayedDigits = 0;
	private int[] correctSequence = null;
	private int positionInSequence;

	// ---------- RED ----------
	int RuleMazeNavigation(int pos)
	{
		/*
			Use the right digit of the display as the starting square in the given maze, and navigate to
			the square that contains the last digit of the serial number in the fewest number of moves
			required.

			If those two digits are the same, press the diagonal arrow that corresponds to the relative
			location of the starting square, and ignore the rest of this section. Otherwise:
			* If the first two moves were along the same axis, start from the arrow in the direction of
			  the first move.
			* If the first two moves were along different axes, start from the diagonal arrow formed by
			  combining the first two moves.

			From the starting arrow, move clockwise a number of steps equal to half the number of moves
			required, rounded down, and press the arrow in that position.
		*/
		int[,] numMoves = new int[,] {
		/* Node   0   1   2   3   4   5   6   7   8   9 */
		/* 0 */ { 0, 20, 31, 16, 18, 27, 11, 12, 26,  9},
		/* 1 */ {20,  0, 19, 14, 12, 15, 13, 10, 14, 13},
		/* 2 */ {31, 19,  0, 25, 23,  6, 24, 21,  9, 24},
		/* 3 */ {16, 14, 25,  0,  8, 21,  9, 12, 20,  9},
		/* 4 */ {18, 12, 23,  8,  0, 19, 11, 10, 18, 11},
		/* 5 */ {27, 15,  6, 21, 19,  0, 20, 17,  5, 20},
		/* 6 */ {11, 13, 24,  9, 11, 20,  0,  9, 19,  4},
		/* 7 */ {12, 10, 21, 12, 10, 17,  9,  0, 16,  7},
		/* 8 */ {26, 14,  9, 20, 18,  5, 19, 16,  0, 19},
		/* 9 */ { 9, 13, 24,  9, 11, 20,  4,  7, 19,  0},
		};

		int[,] direction = new int[,] {
		/* vS G>  0  1  2  3  4  5  6  7  8  9 */
		/* 0 */ { 3, 7, 7, 7, 7, 7, 7, 7, 7, 7},
		/* 1 */ { 2, 7, 2, 2, 2, 2, 2, 2, 2, 2},
		/* 2 */ { 4, 4, 1, 4, 4, 4, 4, 4, 4, 4},
		/* 3 */ { 4, 3, 3, 7, 3, 3, 4, 3, 3, 4},
		/* 4 */ { 1, 1, 1, 1, 7, 1, 1, 1, 1, 1},
		/* 5 */ { 4, 4, 5, 4, 4, 1, 4, 4, 4, 4},
		/* 6 */ { 2, 2, 2, 2, 2, 2, 5, 2, 2, 2},
		/* 7 */ { 6, 7, 7, 7, 7, 7, 6, 3, 7, 6},
		/* 8 */ { 7, 7, 7, 7, 7, 7, 7, 7, 3, 7},
		/* 9 */ { 1, 0, 0, 0, 0, 0, 0, 1, 0, 5},
		};

		int start = displayedDigits % 10;
		int goal = bombInfo.GetSerialNumberNumbers().LastOrDefault();
		int steps = numMoves[start, goal];
		int startDir = direction[start, goal];

		if (steps == 0)
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) Starting position ({1}) and ending position ({2}) are the same.",
				thisLogID, start, goal);
			Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) Press the diagonal arrow corresponding to the relative location of square {1}; the answer is {2}.",
				thisLogID, start, __positionText[startDir]);
		}
		else
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) Navigation from square {1} to square {2} takes {3} steps.",
				thisLogID, start, goal, steps);

			if ((startDir & 1) == 1)
				Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) The first two moves are along different axes. Combine the two arrows and start from that arrow, which is {1}.",
					thisLogID, __positionText[startDir]);
			else
				Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) The first two moves are along the same axis. Start from the first move, which is {1}.",
					thisLogID, __positionText[startDir]);

			steps >>= 1;
			startDir = (startDir + steps) % 8;
			Debug.LogFormat("[Rainbow Arrows #{0}] (North - Maze Navigation) Move {1} steps clockwise to reach the target direction; the answer is {2}.",
				thisLogID, steps, __positionText[startDir]);
		}
		return startDir;
	}

	// ---------- ORANGE ----------
	int RuleArrowDirection(int pos)
	{
		/*
			Multiply the displayed number by four, then modulo 360. Rotate that many degrees clockwise
			from the white arrow, then press the closest arrow. However, if this is the fourth or
			eighth rule used, press the arrow directly opposite of that position instead.
		*/
		int rotation = (displayedDigits * 4) % 360;
		int location = (whiteLocation + (int)Math.Round(rotation / 45.0f)) % 8;
		Debug.LogFormat("[Rainbow Arrows #{0}] (Northeast - Arrow Direction) Rotating {1} degrees from the white arrow brings us closest to the {2} arrow.",
			thisLogID, rotation, __positionText[location]);

		if (pos % 4 == 3)
		{
			location = (location + 4) % 8;
			Debug.LogFormat("[Rainbow Arrows #{0}] (Northeast - Arrow Direction) ... but we want the arrow directly opposite of that one; the answer is {1}.",
				thisLogID, __positionText[location]);
		}
		else
			Debug.LogFormat("[Rainbow Arrows #{0}] (Northeast - Arrow Direction) The answer is {1}.",
				thisLogID, __positionText[location]);

		return location;
	}

	// ---------- YELLOW? ----------
	int RuleBinaryRotation(int pos)
	{
		/*
			Assemble a four-bit binary (base 2) number as follows, with the fourth bit being the most
			significant.
			* Bit 1 is on if any diagonal direction has been pressed in a previous rule.
			* Bit 2 is on if either a parallel port or serial port is present.
			* Bit 3 is on if the displayed number is a multiple of the number of batteries plus one.
			* Bit 4 is on if there are as least as many battery holders as there are port plates.
			Rotate the resulting number right by the number of indicators. Then turn the resulting
			binary number into a decimal number, move clockwise that many steps starting from
			North, and press the arrow in that position.
		*/ 
		uint bits = 0;
		bits |= (uint)(correctSequence.Any(csn => csn != -1 && (csn % 2 == 1)) ? 1 : 0);
		bits |= (uint)(bombInfo.IsPortPresent(Port.Parallel) || bombInfo.IsPortPresent(Port.Serial) ? 2 : 0);
		bits |= (uint)((displayedDigits % (bombInfo.GetBatteryCount() + 1)) == 0 ? 4 : 0);
		bits |= (uint)(bombInfo.GetBatteryHolderCount() >= bombInfo.GetPortPlateCount() ? 8 : 0);

		Debug.LogFormat("[Rainbow Arrows #{0}] (East - Binary Rotation) Base four bit number is 0b{2}, or {1}.",
			thisLogID, bits, Convert.ToString(bits, 2).PadLeft(4, '0'));

		int shiftAmount = bombInfo.GetIndicators().Count() % 4;
		if (shiftAmount != 0) // Simulate rotate right on a 4-bit number (unused bits will be trimmed)
			bits = (bits >> shiftAmount) | (bits << 4 - shiftAmount);

		Debug.LogFormat("[Rainbow Arrows #{0}] (East - Binary Rotation) Rotate right by {3} gives 0b{2}, or {1}.",
			thisLogID, bits & 15, Convert.ToString(bits & 15, 2).PadLeft(4, '0'), shiftAmount);

		Debug.LogFormat("[Rainbow Arrows #{0}] (East - Binary Rotation) Moving {1} step{3} from North gives an answer of {2}.",
			thisLogID, (int)(bits & 15), __positionText[(int)(bits & 7)], (bits & 15) == 1 ? "" : "s");

		return (int)(bits & 7);
	}

	// ---------- GREEN ----------
	int RulePreviousArrows(int pos)
	{
		/*
			If this is the starting rule, press  the white arrow.

			Otherwise, starting from the last arrow that was pressed, move counter-clockwise until an
			arrow that has not been pressed yet is reached. Then, move clockwise a number of steps
			equal to the number of arrows pressed thus far, and press the arrow in that position.
		*/
		if (pos == 0)
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] (Southeast - Previous) This is the first rule, so the answer is the white arrow; that's {1}.",
				thisLogID, __positionText[whiteLocation]);
			return whiteLocation;
		}

		int location = correctSequence[pos-1];
		while (Array.IndexOf(correctSequence, location) != -1)
			location = (location + 7) % 8;

		Debug.LogFormat("[Rainbow Arrows #{0}] (Southeast - Previous) Moving {3} from {2}, the first arrow not pressed is {1}.",
			thisLogID, __positionText[location], __positionText[correctSequence[pos-1]], "counter-clockwise");

		location = (location + pos) % 8;

		Debug.LogFormat("[Rainbow Arrows #{0}] (Southeast - Previous) Moving {2} step{4} {3} from that position leads to an answer of {1}.",
			thisLogID, __positionText[location], pos, "clockwise", pos == 1 ? "" : "s");

		return location;
	}

	// ---------- BLUE ----------
	int RuleLetterString(int pos)
	{
		/*
			Assign every arrow a letter from A-H, starting from the white arrow and proceeding
			clockwise. Then, starting with the string "ABCDEFGH", modify the string
			in the following ways:
			* Move any letters present in the serial number to the start of the string, in the order
			  they first appear in the serial number.
			* If the last digit of the serial number is even, rotate the string forward by the first
			  digit of the serial number.
			* If there is an odd number of batteries, move all letters in odd positions to the front
			  of the string, keeping the order they appeared.
			* If North or South has been assigned a vowel, reverse the string. 
			Afterwards, press the arrow that the nth letter in the string was assigned to, where n is
			the digital root of the displayed number, minus one. (If n is 0 or less, use 1.)
		*/
		string letterStr = "";
		foreach (char c in bombInfo.GetSerialNumber().Distinct().Where((ch) => ch >= 'A' && ch <= 'H'))
			letterStr += c;
		for (int i = 0; i < 8; ++i)
		{
			if (!letterStr.Contains("ABCDEFGH"[i]))
				letterStr += "ABCDEFGH"[i];
		}
		Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) After modification 1, the string is \"{1}\".",
			thisLogID, letterStr);

		if (bombInfo.GetSerialNumberNumbers().LastOrDefault() % 2 == 0)
		{
			int rotation = bombInfo.GetSerialNumberNumbers().FirstOrDefault() % 8;
			if (rotation != 0)
			{
				string tmp = letterStr.Substring(8 - rotation);
				tmp += letterStr.Substring(0, 8 - rotation);
				letterStr = tmp;
			}
			Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) After modification 2, the string is \"{1}\".",
				thisLogID, letterStr);
		}

		if (bombInfo.GetBatteryCount() % 2 == 1)
		{
			// Remember we're zero based. "Odd positions" is the first, third, fifth, and seventh.
			string tmp1 = "" + letterStr[0] + letterStr[2] + letterStr[4] + letterStr[6];
			string tmp2 = "" + letterStr[1] + letterStr[3] + letterStr[5] + letterStr[7];
			letterStr = tmp1 + tmp2;

			Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) After modification 3, the string is \"{1}\".",
				thisLogID, letterStr);
		}

		if (whiteLocation == 0 || whiteLocation == 4)
		{
			letterStr = new string(letterStr.Reverse().ToArray());
			Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) After modification 4, the string is \"{1}\".",
				thisLogID, letterStr);
		}

		// "Digital Root"
		int index = displayedDigits == 0 ? 0 : ((displayedDigits - 1) % 9) + 1;
		if (--index <= 0)
			index = 1;

		Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) We want character {1} in the final string, and that's {2}.",
			thisLogID, index, letterStr[index - 1]);

		int location = whiteLocation + (letterStr[index - 1] - 'A');

		Debug.LogFormat("[Rainbow Arrows #{0}] (South - Letter String) The assigned arrow that corresponds to letter {2} is {1}.",
			thisLogID, __positionText[location % 8], letterStr[index - 1]);		
		return location % 8;
	}

	// ---------- INDIGO? ----------
	int RuleHeadCount(int pos)
	{
		/*
			For each word in the given table, determine a value equal to the count of letters in the word
			that are present in the serial number, plus the number of modules on the bomb named 
			"[WORD] Arrows". Take the word with the highest value and look up the number next to that
			word in the table; if there's a tie, use the word with the lowest number.

			Starting from the white arrow, move that many steps in the same direction as the rainbow
			pattern, and press the arrow in that position. 
		*/

		char[] snLetters = bombInfo.GetSerialNumber().ToLower().Distinct().ToArray();
		List<string> modules = bombInfo.GetModuleNames();
		string[] colors = new string[] {"red", "orange", "yellow", "green", "blue", "indigo", "purple", "double"};

		int curAnswer = 1, curBestValue = -1;


		for (int i = 0; i < 8; ++i)
		{
			string color = colors[i];
			int moduleMatches = modules.FindAll(mod => mod.ToLower() == String.Format("{0} arrows", color)).Count;
			int serialMatches = 0;
			foreach (char c in color.ToCharArray())
			{
				if (snLetters.Contains(c))
					++serialMatches;
			}

			if (curBestValue < moduleMatches + serialMatches)
			{
				curBestValue = moduleMatches + serialMatches;
				curAnswer = i + 1;
			}
			Debug.LogFormat("[Rainbow Arrows #{0}] (Southwest - Head Count) For the word \"{1}\", {2} serial number matches + {3} module matches = value of {4}.",
				thisLogID, color, serialMatches, moduleMatches, moduleMatches + serialMatches);
		}

		int finalAnswer = (curAnswer * ((ccwRainbow) ? 7 : 1) + whiteLocation) % 8;
		Debug.LogFormat("[Rainbow Arrows #{0}] (Southwest - Head Count) The best value obtained was from the word \"{1}\", so move {2} step{3} from the white arrow; the answer is {4}.",
			thisLogID, colors[curAnswer-1], curAnswer, curAnswer==1?"":"s", __positionText[finalAnswer]);			
		return finalAnswer;
	}

	// ---------- VIOLET (PURPLE) ----------
	int RuleAdjacentWords(int pos)
	{
		/*
			Using the table, find the word that shares the most letters in common with the serial number.
			If there are multiple, use the first in reading order. Then, take the nth letter of the 
			English alphabet, where n is the displayed number modulo 26, plus one.

			If one of the words adjacent to the given word (orthogonally or diagonally) starts with that
			letter, press the arrow in the direction that would take you to that word. Otherwise, use the
			next letter of the English alphabet, wrapping around from Z to A if necessary, and repeat
			until such a word is found. The table does not wrap around.
		*/
		string[] wordTable = new string[] {
			"yoked", "white", "poets", "xysti",
			"lower", "tango", "magic", "joust",
			"farce", "along", "quirk", "hotel",
			"zeros", "royal", "bravo", "vault"
		};
		string[] validMoves = new string[] {
			  "234",   "23456",    "23456",   "456",
			"01234", "01234567", "01234567", "04567",
			"01234", "01234567", "01234567", "04567",
			"012",   "01267",    "01267",    "067"
		};
		int[] directions = new int[] {
			-4, -3, 1, 5, 4, 3, -1, -5
		};

		int mostCharsIndex = 0;
		{
			int mostCharsInCommon = 0;
			char[] snLetters = bombInfo.GetSerialNumber().ToLower().Distinct().ToArray();
			for (int i = 0; i < wordTable.Length; ++i)
			{
				int numCharsInCommon = wordTable[i].ToCharArray().Intersect(snLetters).ToArray().Length;
				if (numCharsInCommon > mostCharsInCommon)
				{
					mostCharsInCommon = numCharsInCommon;
					mostCharsIndex = i;
				}
			}			

			Debug.LogFormat("[Rainbow Arrows #{0}] (West - Adjacent Words) The word with the most letters in common with the serial number is \"{1}\", with {2}.",
				thisLogID, wordTable[mostCharsIndex], mostCharsInCommon);
		}

		string check = "abcdefghijklmnopqrstuvwxyz";
		if (displayedDigits % 26 != 0)
		{
			check  = "abcdefghijklmnopqrstuvwxyz".Substring(displayedDigits % 26);
			check += "abcdefghijklmnopqrstuvwxyz".Substring(0, displayedDigits % 26);
		}

		Debug.LogFormat("[Rainbow Arrows #{0}] (West - Adjacent Words) The starting letter is '{1}'.",
			thisLogID, check[0]);

		int answer = 0;
		int distance = 27;
		int whichWord = 0;
		foreach (char c in validMoves[mostCharsIndex])
		{
			int i = c - '0';
			int ourDistance = check.IndexOf(wordTable[mostCharsIndex + directions[i]][0]);
			if (ourDistance < distance)
			{
				distance = ourDistance;
				answer = i;
				whichWord = mostCharsIndex + directions[i];
			}
		}

		Debug.LogFormat("[Rainbow Arrows #{0}] (West - Adjacent Words) The letter '{2}' gives us a match in the {3} direction ('{1}'), so the answer is {3}.",
			thisLogID, wordTable[whichWord], wordTable[whichWord][0], __positionText[answer]);

		return answer;
	}

	// ---------- WHITE ----------
	int RuleBasicAppearance(int pos)
	{
		/*
			If both digits on the module are present in the bomb's serial number, press North. If only
			the left digit is present, press West. If only the right digit is present, press East. If
			neither are present, press South.
		*/
		int[] digits = bombInfo.GetSerialNumberNumbers().ToArray();
		int result = ((Array.IndexOf(digits, displayedDigits / 10) != -1) ? 2 : 0) | ((Array.IndexOf(digits, displayedDigits % 10) != -1) ? 1 : 0);
		switch (result)
		{
			case 3: 
				Debug.LogFormat("[Rainbow Arrows #{0}] (Northwest - Appearance) Both digits present. The answer is north.", thisLogID);
				return 0; // North
			case 2: 
				Debug.LogFormat("[Rainbow Arrows #{0}] (Northwest - Appearance) Left digit present. The answer is west.", thisLogID);
				return 6; // West
			case 1:
				Debug.LogFormat("[Rainbow Arrows #{0}] (Northwest - Appearance) Right digit present. The answer is east.", thisLogID);
				return 2; // East
		}
		Debug.LogFormat("[Rainbow Arrows #{0}] (Northwest - Appearance) Neither digit present. The answer is south.", thisLogID);
		return 4; // South
	}

	// -----

	void ResetInput()
	{
		positionInSequence = 0;

		if (currentCoroutine != null)
			StopCoroutine(currentCoroutine);
		currentCoroutine = StartCoroutine(StartupAnimation());
	}

	void GenerateSolution()
	{
		positionInSequence = 0;
		displayedDigits = RNG.Range(0, 100);
		correctSequence = new int[] { -1, -1, -1, -1, -1, -1, -1, -1 };

		Debug.LogFormat("[Rainbow Arrows #{0}] The display shows \"{1:00}\".", thisLogID, displayedDigits);

		List<Func<int, int>> sectionRules = new List<Func<int, int>>() {
			/* North      */ RuleMazeNavigation,
			/* North-East */ RuleArrowDirection,
			/*       East */ RuleBinaryRotation,
			/* South-East */ RulePreviousArrows,
			/* South      */ RuleLetterString,
			/* South-West */ RuleHeadCount,
			/*       West */ RuleAdjacentWords,
			/* North-West */ RuleBasicAppearance
		};

		// Start on the rule indicated by white, then proceed in the direction of the rainbow.
		int nextToAdd, rule = whiteLocation;
		for (int i = 0; i < correctSequence.Length; ++i)
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] ----------", thisLogID);
			nextToAdd = sectionRules[rule](i);

			int oldNext = nextToAdd;
			int uq = 0;
			for (; uq < 8; ++uq)
			{
				if (ccwRainbow)
					nextToAdd = oldNext + __uniquenessOrderCCW[uq];
				else
					nextToAdd = oldNext + __uniquenessOrderCW[uq];
				nextToAdd %= 8;
				if (Array.IndexOf(correctSequence, nextToAdd) == -1)
					break;
			}

			if (oldNext != nextToAdd)
				Debug.LogFormat("[Rainbow Arrows #{0}] The above rule returned {2}, which wasn't unique. The closest unique arrow was {1}.", 
					thisLogID, __positionText[nextToAdd], __positionText[oldNext]);

			correctSequence[i] = nextToAdd;

			rule = (rule + (ccwRainbow ? 7 : 1)) % 8;
		}

		startColor = RNG.Range(0.0f, 1.0f);
		display.color = Color.HSVToRGB(startColor, 1.0f, 1.0f);

		if (currentCoroutine != null)
			StopCoroutine(currentCoroutine);
		currentCoroutine = StartCoroutine(StartupAnimation());

		string debugOutSequence = "The full correct sequence is: ";
		for (int i = 0; i < correctSequence.Length; ++i)
			debugOutSequence += __positionText[correctSequence[i]] + ((i != 7) ? ", " : ".");

		Debug.LogFormat("[Rainbow Arrows #{0}] ----------", thisLogID);
		Debug.LogFormat("[Rainbow Arrows #{0}] {1}", thisLogID, debugOutSequence);
	}

	void RandomizeArrows()
	{
		int rand = RNG.Range(0, 16);
		ccwRainbow = (rand & 8) == 8;
		whiteLocation = (rand & 7);

		Debug.LogFormat("[Rainbow Arrows #{0}] The white arrow is facing {1}, and the rainbow direction is {2}.",
			thisLogID, __positionText[whiteLocation], ccwRainbow ? "counter-clockwise" : "clockwise");

		for (int i = whiteLocation, j = 0; j < 8; ++j)
		{
			i = (i + (ccwRainbow ? 7 : 1)) % 8;
			arrowButtons[i].GetComponent<Renderer>().material = colorMeshes[j];
		}
	}


	// -----
	// The Dirty Work™
	// -----

	bool ButtonPressed(int button)
	{
		arrowButtons[button].AddInteractionPunch(0.25f);
		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

		if (moduleSolved || correctSequence == null)
			return false;

		if (button != correctSequence[positionInSequence])
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] STRIKE: For input #{1}, you pressed {2} when I expected {3}. Input reset.",
				thisLogID, positionInSequence+1, __positionText[button], __positionText[correctSequence[positionInSequence]]);
			bombModule.HandleStrike();
			ResetInput();
			return false;
		}
		if (++positionInSequence >= correctSequence.Length)
		{
			Debug.LogFormat("[Rainbow Arrows #{0}] SOLVE: Button sequence has been input correctly.", thisLogID);
			moduleSolved = true;

			if (currentCoroutine != null)
				StopCoroutine(currentCoroutine);
			currentCoroutine = StartCoroutine(SolveAnimation());
		}
		return false;
	}

	void Awake()
	{
		thisLogID = ++globalLogID;

		for (int i = 0; i < arrowButtons.Length; ++i)
		{
			int j = i;
			arrowButtons[i].OnInteract += delegate() {
				return ButtonPressed(j);
			};
		}

		RandomizeArrows();
		display.text = "";

		bombModule.OnActivate += GenerateSolution;
	}


	// -----
	// Twitch Plays support
	// -----

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Press arrows with '!{0} press UL R D', '!{0} press NW E S', or '!{0} press 7 6 2' (keypad order—8 is North). The word 'press' is optional, but spacing is important.";
#pragma warning restore 414

	public IEnumerator ProcessTwitchCommand(string command)
	{
		List<string> cmds = command.Split(' ').ToList();
		List<int> presses = new List<int>();
		if (cmds.Count > 9)
			yield break;

		for (int i = 0; i < cmds.Count; ++i)
		{
			if (Regex.IsMatch(cmds[i], @"^(press|select)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
			{
				if (i == 0)
					continue; // Ignore filler press/select at the start
				yield break;
			}
			if      (Regex.IsMatch(cmds[i], @"^(?:U|T|TM|up|top|N|north|8)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(0);
			else if (Regex.IsMatch(cmds[i], @"^(?:[UT]R|(?:up|top)-?right|NE|north-?east|9)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(1);
			else if (Regex.IsMatch(cmds[i], @"^(?:MR|R|right|E|east|6)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(2);
			else if (Regex.IsMatch(cmds[i], @"^(?:[DB]R|(?:down|bottom)-?right|SE|south-?east|3)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(3);
			else if (Regex.IsMatch(cmds[i], @"^(?:D|B|BM|down|bottom|S|south|2)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(4);
			else if (Regex.IsMatch(cmds[i], @"^(?:[DB]L|(?:down|bottom)-?left|SW|south-?west|1)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(5);
			else if (Regex.IsMatch(cmds[i], @"^(?:ML|L|left|W|west|4)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(6);
			else if (Regex.IsMatch(cmds[i], @"^(?:[UT]L|(?:up|top)-?left|NW|north-?west|7)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				presses.Add(7);
			else if (Regex.IsMatch(cmds[i], @"^(?:M|MM|middle|middlemiddle|C|center|5)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
			{
				yield return String.Format("sendtochaterror Rainbows don't have a center...");
				yield break;
			}
			else
			{
				yield return String.Format("sendtochaterror I'm looking for a direction, what the heck is this '{0}' nonsense you gave me?", cmds[i]);
				yield break;
			}
		}
		if (presses.Count > 0)
		{
			yield return null;
			if (presses.Count == 1)
				yield return new KMSelectable[] { arrowButtons[presses[0]] };
			else for (int i = 0; i < presses.Count; ++i)
			{
				yield return String.Format("strikemessage pressing {0} (input #{1})", __positionText[presses[i]], i + 1);
				yield return new KMSelectable[] { arrowButtons[presses[i]] };
			}

			if (moduleSolved)
				yield return "solve";
		}
		yield break;
	}

	public IEnumerator TwitchHandleForcedSolve()
	{
		if (moduleSolved)
			yield break;

		Debug.LogFormat("[Rainbow Arrows #{0}] Force solve requested by Twitch Plays.", thisLogID);
		while (!moduleSolved)
		{
			arrowButtons[correctSequence[positionInSequence]].OnInteract();
			yield return new WaitForSeconds(0.125f);
		}
		while (currentCoroutine != null)
			yield return true;
	}
}
