/*
 * TieTextEditor.exe, Allows the editing of TEXT resources and STRINGS.DAT from TIE
 * Copyright (C) 2006-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * VERSION: 1.4
 */

/* CHANGELOG
 * v1.4, 260314
 * [FIX #2] STRINGS Save button, R/W overhaul, special chars
 * [NEW #2] Added Ship#.lfd
 * v1.3, 230120
 * [NEW] Added TieText 1 thru 3, Title.lfd
 * [UPD] Rewrote to fully use LfdReader
 * v1.2, 220824
 * [UPD] Updated from legacy
 */

using Idmr.LfdReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Idmr.TieTextEditor
{
	/// <summary>
	/// Reads and edits STRINGS.DAT, TieText#.lfd, and Shipset#.lfd
	/// 
	/// S buttons for navigating 1/20/100 strings at a time
	/// T buttons for 1/5/20
	/// Sh buttons for 1/5/10
	/// tabs seperating files
	/// 
	/// 'S' prefix denotes STRINGS.DAT
	/// 'T' prefix denotes TieText#.lfd
	/// 'Sh' prefix denotes Shipset#.lfd (all-in-one)
	/// </summary>
	public partial class MainForm : Form
	{
		// Strings
		int _activeString;
		string _stringsOriginal;
		readonly string[] _strings = new string[695];
		readonly string _strFile;
		// TieText
		int _currentTieTextFile;
		int _activeTieText;
		string[] _tieTextSubstrings;
		//long _tieTextOffset;
		int _currentTTArray = 2;
		Text _text;
		// Shipset
		int _activeShipset;
        readonly TextBox[] _shipsetTextBoxes;
		LfdFile _shipset;
		// Ship
		int _activeShip = 1;
		Text _ship;
		int _activeMiss = -1;
        // other
        readonly string _filePath;
        int _titleOriginalLength;
		string _titleOriginal;

		public MainForm()
		{
			InitializeComponent();
			string settingsFile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
				+ "\\Imperial Department of Military Research\\TTE\\Settings.ini";
            if (!File.Exists(settingsFile))
			{
				var result = fldTie.ShowDialog();
				if (result == DialogResult.OK)
				{
					_filePath = fldTie.SelectedPath;
					Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
					using (StreamWriter sw = new FileInfo(settingsFile).CreateText())
					{
						sw.WriteLine(_filePath);
					}
				}
			}
			else
			{
				using (StreamReader sr = File.OpenText(settingsFile))
				{
					_filePath = sr.ReadLine();
				}
			}
			if (!Directory.Exists(_filePath))
			{
				File.Delete(settingsFile);
				throw new DirectoryNotFoundException();
			}

			//Strings--------
			_strFile = "C:\\Users\\Me\\Downloads\\strings\\it_STRINGS.DAT";
			//_strFile = _filePath + "\\STRINGS.DAT";
			readStrings();
			//TieText--------
			loadTieText();
			//Shipset1-------
			_shipsetTextBoxes = new TextBox[6];
			_shipsetTextBoxes[0] = txtLine1;
			_shipsetTextBoxes[1] = txtLine2;
			_shipsetTextBoxes[2] = txtLine3;
			_shipsetTextBoxes[3] = txtLine4;
			_shipsetTextBoxes[4] = txtLine5;
			_shipsetTextBoxes[5] = txtLine6;
			_shipset = new LfdFile(_filePath + "\\Resource\\Shipset1.lfd");
			readShipset();
			//Title----------
			var title = new LfdFile(_filePath + "\\Resource\\Title.lfd");
            Text text = (Text)title.Resources[5];
			_titleOriginal = text.Strings[0];
			_titleOriginalLength = _titleOriginal.Length;
            txtTitle.Text = text.Strings[0].Replace("\n", "\r\n").Replace("\0", "\r\n");
			//Ship-----------
			loadShip();
		}

		#region Strings
		void readStrings()
		{
			using (FileStream fs = File.OpenRead(_strFile))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					for (int i = 0; i < _strings.Length; i++)
					{
						fs.Position = i * 4;
						int pos = br.ReadInt32();
						fs.Position = pos;
						int len = 0;
						while (fs.ReadByte() != 0) len++;
						fs.Position = pos;
						_strings[i] = "";
						for (int j = 0; j < len; j++)
						{
							byte chr = br.ReadByte();
							if (chr < 0x20 || chr > 0x7a) _strings[i] += $"\\x{chr:X2}";
							else _strings[i] += (char)chr;
						}
					}
				}
			}
			getString();
		}
		void getString()
		{
			_stringsOriginal = _strings[_activeString];
			lblSPos.Text = (_activeString + 1) + " / 695";
			txtString.Text = _stringsOriginal;
		}
		
		void cmdSPrevClick(object sender, EventArgs e)
		{	//Prev/Next are by 1
			if (_activeString != 0) _activeString--;
			getString();
		}
		void cmdSNextClick(object sender, EventArgs e)
		{
			if (_activeString != 694) _activeString++;
			getString();
		}		
		void cmdSPrev2Click(object sender, EventArgs e)
		{	//Prev2/Next2 are by 20
			if (_activeString < 20) _activeString = 0; else _activeString -= 20;
			getString();
		}
		void cmdSNext2Click(object sender, EventArgs e)
		{
			if (_activeString > 674) _activeString = 694; else _activeString += 20;
			getString();
		}
		void cmdSPrev3Click(object sender, EventArgs e)
		{	//Prev3/Next3 are by 100
			if (_activeString < 100) _activeString = 0; else _activeString -= 100;
			getString();
		}
		void cmdSNext3Click(object sender, EventArgs e)
		{
			if (_activeString > 594) _activeString = 694; else _activeString += 100;
			getString();
		}

		void cmdSaveStrings_Click(object sender, EventArgs e)
		{
			File.Copy(_strFile, Path.ChangeExtension(_strFile, ".bak"), true);
			File.Delete(_strFile);
			using (FileStream fs = File.OpenWrite(_strFile))
			{
				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					uint offset = 0xAE0;
					for (int i = 0; i < _strings.Length; i++)
					{
						fs.Position = i * 4;
						bw.Write(offset);
						fs.Position = offset;
						if (_strings[i].Contains("\\x"))
						{
							List<byte> arr = new List<byte>();
							for (int c = 0; c < _strings[i].Length; c++)
							{
								try
								{
									if (_strings[i][c] == '\\' && _strings[i][c + 1] == 'x')
									{
										c += 2;
										arr.Add(byte.Parse(_strings[i].Substring(c, 2), System.Globalization.NumberStyles.HexNumber));
										c++; // loop covers the next char
									}
									else arr.Add((byte)_strings[i][c]);
								}
								catch { arr.Add((byte)_strings[i][c]); }
							}
							bw.Write(arr.ToArray());
						}
						else bw.Write(_strings[i].ToCharArray());
						bw.Flush();
						fs.WriteByte(0);
						offset = (uint)fs.Position;
					}
					fs.SetLength(fs.Position);
				}
				
			}
		}
		#endregion

		#region TieText
		void loadTieText()
		{
			var tt = new LfdFile($"{_filePath}\\Resource\\TieText{_currentTieTextFile}.lfd");
			_text = (Text)tt.Resources[_currentTieTextFile == 0 ? 1 : 0];
            tabTieText.Text = "TieText" + _currentTieTextFile + ".lfd";
            _tieTextSubstrings = _text.Strings[_currentTTArray].Split('\0');
            readTieText();
			cmdPrevArray.Enabled = (_currentTTArray != 0);
			cmdNextArray.Enabled = (_currentTTArray + 1 != _text.NumberOfStrings);
        }
		
		void updateTieText()
		{
			if (txtTieText.Text == _tieTextSubstrings[_activeTieText]) return;

			_tieTextSubstrings[_activeTieText] = txtTieText.Text;
			string fullStr = string.Join("\0", _tieTextSubstrings);
			_text.Strings[_currentTTArray] = fullStr;
			_text.EncodeResource();
			var lfd = new LfdFile(_filePath + "\\Resource\\TieText" + _currentTieTextFile + ".lfd");
			lfd.Resources[_currentTieTextFile == 0 ? 1 : 0] = _text;
			lfd.Write();
		}
		
		void readTieText()
		{
			txtTieText.Text = _tieTextSubstrings[_activeTieText];
			lblTPos.Text = (_activeTieText + 1) + " / " + _tieTextSubstrings.Length;
		}
		
		void cmdTNextClick(object sender, EventArgs e)
		{
			updateTieText();
			if (_activeTieText != _tieTextSubstrings.Length - 1) _activeTieText++;
			readTieText();
		}
		void cmdTPrevClick(object sender, EventArgs e)
		{
			updateTieText();
			if (_activeTieText != 0) _activeTieText--;
			readTieText();
		}
		void cmdTNext2Click(object sender, EventArgs e)
		{
			updateTieText();
			if (_activeTieText < _tieTextSubstrings.Length - 5) _activeTieText += 5;
			else _activeTieText = _tieTextSubstrings.Length - 1;
            readTieText();
		}
		void cmdPrev2Click(object sender, EventArgs e)
		{
			updateTieText();
			if (_activeTieText >= 5) _activeTieText -= 5;
			else _activeTieText = 0;
			readTieText();
		}
		void cmdTNext3Click(object sender, EventArgs e)
		{
			updateTieText();
            if (_activeTieText < _tieTextSubstrings.Length - 20) _activeTieText += 20;
            else _activeTieText = _tieTextSubstrings.Length - 1;
            readTieText();
		}
		void cmdPrev3Click(object sender, EventArgs e)
		{
			updateTieText();
            if (_activeTieText >= 20) _activeTieText -= 20;
            else _activeTieText = 0;
            readTieText();
		}

        void cmdTiePrev_Click(object sender, EventArgs e)
        {
			if (_currentTieTextFile == 0) return;

			_currentTieTextFile--;
			cmdTieNext.Enabled = true;
			cmdTiePrev.Enabled = (_currentTieTextFile != 0);
			if (_currentTieTextFile == 0) _currentTTArray = 2;
			else _currentTTArray = 0;
			loadTieText();
        }
        void cmdTieNext_Click(object sender, EventArgs e)
        {
			if (_currentTieTextFile == 3) return;

			_currentTieTextFile++;
			cmdTiePrev.Enabled = true;
			cmdTieNext.Enabled = (_currentTieTextFile != 3);
			_currentTTArray = 0;
			loadTieText();
        }

        void cmdPrevArray_Click(object sender, EventArgs e)
        {
			if (_currentTTArray == 0) return;

			_currentTTArray--;
            _tieTextSubstrings = _text.Strings[_currentTTArray].Split('\0');
			_activeTieText = 0;
            readTieText();
			cmdPrevArray.Enabled = (_currentTTArray != 0);
			cmdNextArray.Enabled = true;
        }
        void cmdNextArray_Click(object sender, EventArgs e)
        {
			if (_currentTTArray == _text.NumberOfStrings - 1) return;

			_currentTTArray++;
            _tieTextSubstrings = _text.Strings[_currentTTArray].Split('\0');
			_activeTieText = 0;
            readTieText();
            cmdPrevArray.Enabled = true;
            cmdNextArray.Enabled = (_currentTTArray + 1 != _text.NumberOfStrings);
        }
        #endregion

        #region length labels
        void txtStringTextChanged(object sender, EventArgs e)
		{
			lblSCount.Text = (txtString.Text.Length - _stringsOriginal.Length).ToString();
			_strings[_activeString] = txtString.Text;
		}
		void txtTieTextTextChanged(object sender, EventArgs e)
		{
			lblTCount.Text = (txtTieText.Text.Length - _tieTextSubstrings[_activeTieText].Length).ToString();
		}
        void txtShipset_TextChanged(object sender, EventArgs e)
        {
            int count;
            count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length +
                txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - ((Text)_shipset.Resources[0]).Strings[_activeShipset].TrimEnd('\0').Length + 2;
			for (int i = 1; i < 6; i++) if (_shipsetTextBoxes[i].Text != "") count++;
            lblShCount.Text = count.ToString();
        }
        void txtTitle_TextChanged(object sender, EventArgs e)
        {
			lblTitleCount.Text = (txtTitle.Text.Replace("\r\n", "\0").Replace("\0\0\0", "\0\n\0").Length - _titleOriginalLength).ToString();
        }
        void lblCount_TextChanged(object sender, EventArgs e)
        {
			var lbl = (Label)sender;
            if (lbl.Text == "0") lbl.ForeColor = System.Drawing.Color.Lime;
            else lbl.ForeColor = System.Drawing.Color.Red;
        }
		#endregion

		#region Shipset
		void updateShipset()
		{
            var text = (Text)_shipset.Resources[0];
            string[] substrings = text.Strings[_activeShipset].Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            bool linesUnmodified = true;
			for (int i = 0; i < 6; i++) if (substrings.Length > i + 2 && _shipsetTextBoxes[i].Text != substrings[i + 2]) linesUnmodified = false;	//check existing
			for (int i = substrings.Length - 2; i < 6; i++) if (_shipsetTextBoxes[i].Text != "") linesUnmodified = false;	// check for adds
			if (txtName.Text == substrings[0] && txtOPT.Text == substrings[1] && linesUnmodified) return;

            List<string> usedStrings = new List<string>
            {
                txtName.Text,
                txtOPT.Text
            };
            for (int i = 0; i < 6; i++)
				if (_shipsetTextBoxes[i].Text != "") usedStrings.Add(_shipsetTextBoxes[i].Text);
			text.Strings[_activeShipset] = string.Join("\0", usedStrings.ToArray());
			text.EncodeResource();
			_shipset.Write();
		}
		
		void readShipset()
		{
			var text = (Text)_shipset.Resources[0];
			string[] substrings = text.Strings[_activeShipset].Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            txtName.Text = substrings[0];
			txtOPT.Text = substrings[1];
			for (int i = 0; i < 6; i++)
				_shipsetTextBoxes[i].Text = substrings.Length > i + 2 ? substrings[i + 2] : "";
			lblShPos.Text = (_activeShipset + 1) + " / " + text.NumberOfStrings;
		}
		
		void cmdShNextClick(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset != ((Text)_shipset.Resources[0]).NumberOfStrings - 1) _activeShipset++;
			readShipset();
		}
		void cmdShPrevClick(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset != 0) _activeShipset--;
			readShipset();
		}
		void cmdShNext2Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset < (((Text)_shipset.Resources[0]).NumberOfStrings - 5)) _activeShipset += 5;
			else _activeShipset = ((Text)_shipset.Resources[0]).NumberOfStrings - 1;
			readShipset();
		}
		void cmdShPrev2Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset >= 5) _activeShipset -= 5;
			else _activeShipset = 0;
			readShipset();
		}
		void cmdShNext3Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset < (((Text)_shipset.Resources[0]).NumberOfStrings - 10)) _activeShipset += 10;
			else _activeShipset = ((Text)_shipset.Resources[0]).NumberOfStrings - 1;
			readShipset();
		}
		void cmdShPrev3Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset >= 10) _activeShipset -= 10;
			else _activeShipset = 0;
			readShipset();
		}
		void cmdFileNextClick(object sender, EventArgs e)
		{
			updateShipset();
			_activeShipset = 0;
            tabShipset.Text = "Shipset2.lfd";
            _shipset = new LfdFile(_filePath + "\\Resource\\" + tabShipset.Text);
            readShipset();
			cmdFileNext.Enabled = false;
			cmdFilePrev.Enabled = true;
		}
		void cmdFilePrevClick(object sender, EventArgs e)
		{
			updateShipset();
			_activeShipset = 0;
            tabShipset.Text = "Shipset1.lfd";
            _shipset = new LfdFile(_filePath + "\\Resource\\" + tabShipset.Text);
            readShipset();
			cmdFileNext.Enabled = true;
			cmdFilePrev.Enabled = false;
		}
		void cmdNewClick(object sender, EventArgs e)
		{
			// TODO: allow inserting new entries
		}
		void cmdDelClick(object sender, EventArgs e)
		{
			// TODO: allow deleting entries
		}
        #endregion

		void cmdSaveTitle_Click(object sender, EventArgs e)
        {
            var title = new LfdFile(_filePath + "\\Resource\\Title.lfd");
            Text text = (Text)title.Resources[5];
			text.Strings[0] = txtTitle.Text.Replace("\r\n", "\0").Replace("\0\0\0", "\0\n\0");
			text.EncodeResource();
			title.Write();
			_titleOriginal = text.Strings[0].TrimEnd('\0');
			_titleOriginalLength = _titleOriginal.Length;
			txtTitle_TextChanged("Save", new EventArgs());
        }

		#region Ship
		void loadShip()
		{
			tabShip.Text = $"Ship{_activeShip}.lfd";
			var shipFile = new LfdFile($"{_filePath}\\Resource\\Ship{_activeShip}.lfd");
			_ship = (Text)shipFile.Resources[0];
			txtShipName.Text = _ship.Strings[0];
			string[] subs = _ship.Strings[1].Split('\0');
			txtShipSpec.Text = subs[0];
			txtShipOpt.Text = subs[1];
			txtShipNums1.Text = subs[2];
			subs = _ship.Strings[2].Split('\0');
			txtShipSpec2.Text = subs[0];
			txtShipOpt2.Text = subs[1];
			txtShipNums2.Text = subs[2];
			subs = _ship.Strings[3].Split('\0');
			txtTechSpec.Text = subs[0];
			txtTechOpt.Text = subs[1];
			txtStats.Text = "";
			for (int i = 2; i < subs.Length; i++)
			{
				txtStats.Text += subs[i];
				if (i < subs.Length - 1) txtStats.Text += "\r\n";
			}
			subs = _ship.Strings[4].Split('\0');
			txtLaunch.Text = subs[0];
			txtWeapon.Text = subs[1];
			subs = _ship.Strings[5].Split('\0');
			lstMiss.Items.Clear();
			for (int i = 0; i < subs.Length; i++) lstMiss.Items.Add(subs[i]);
			lstMiss.SelectedIndex = 0;
		}

		void chkDanger_CheckedChanged(object sender, EventArgs e)
		{
			var state = chkDanger.Checked;
			txtShipSpec.Enabled = state;
			txtShipOpt.Enabled = state;
			txtShipNums1.Enabled = state;
			txtShipSpec.Enabled = state;
			txtShipOpt2.Enabled = state;
			txtShipNums2.Enabled = state;
			txtShipSpec2.Enabled = state;
			txtLaunch.Enabled = state;
			txtWeapon.Enabled = state;
			txtTechOpt.Enabled = state;
			txtTechSpec.Enabled = state;
		}

		void cmdNextShip_Click(object sender, EventArgs e)
		{
			if (_activeShip < 7)
			{
				_activeShip++;
				cmdPrevShip.Enabled = true;
			}
			if (_activeShip == 7) cmdNextShip.Enabled = false;
			loadShip();
		}
		void cmdPrevShip_Click(object sender, EventArgs e)
		{
			if (_activeShip > 1)
			{
				_activeShip--;
				cmdNextShip.Enabled = true;
			}
			if (_activeShip == 1) cmdPrevShip.Enabled = false;
			loadShip();
		}
		void cmdSaveShip_Click(object sender, EventArgs e)
		{
			_ship.Strings[0] = txtShipName.Text;
			_ship.Strings[1] = txtShipSpec.Text + '\0' + txtShipOpt.Text + '\0' + txtShipNums1.Text;
			_ship.Strings[2] = txtShipSpec2.Text + '\0' + txtShipOpt2.Text + '\0' + txtShipNums2.Text;
			// reminder: don't need to worry about trailing \0's, as Encode will address that
			var str = txtStats.Text.Replace("\r\n", "\0").Replace("\0\0", "\0");	// remove blank lines
			_ship.Strings[3] = txtTechSpec.Text + '\0' + txtTechOpt.Text + str;
			_ship.Strings[4] = txtLaunch.Text + '\0' + txtWeapon.Text;
			_ship.Strings[5] = "";
			for (int i = 0; i < lstMiss.Items.Count; i++)
				_ship.Strings[5] += lstMiss.Items[i].ToString() + '\0';
			lstMiss_SelectedIndexChanged("save", new EventArgs());  // force update the current
			_ship.EncodeResource();
			var shipFile = new LfdFile($"{_filePath}\\Resource\\Ship{_activeShip}.lfd");
			shipFile.Resources[0] = _ship;
			shipFile.Write();
		}

		void lstMiss_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_activeMiss != -1) _ship.Strings[6 + _activeMiss] = txtMiss.Text.Replace("\r\n", "\0");
			_activeMiss = lstMiss.SelectedIndex;
			txtMiss.Text = _ship.Strings[6 + _activeMiss].Replace("\0", "\r\n");
		}
		#endregion
	}
}