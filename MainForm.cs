/*
 * TieTextEditor.exe, Allows the editing of TEXT resources and STRINGS.DAT from TIE
 * Copyright (C) 2006-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * VERSION: 1.3
 */

/* CHANGELOG
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
		// TieText
		int _currentTieTextFile;
		int _activeTieText;
		string[] _tieTextSubstrings;
		long _tieTextOffset;
		int _currentTTArray = 2;
		Text _text;
		// Shipset
		int _activeShipset;
        readonly TextBox[] _shipsetTextBoxes;
		LfdFile _shipset;
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
        }

		void updateStrings()    //writes new string to file, and updates file if neccessary
		{
			if (txtString.Text == _stringsOriginal) return; //ignore if no changes

			using (FileStream fs = File.Open(_filePath + "\\STRINGS.DAT", FileMode.Open, FileAccess.ReadWrite))
			{
				BinaryReader br = new BinaryReader(fs);
				BinaryWriter bw = new BinaryWriter(fs);
				fs.Position = _activeString * 4;
				uint offset = br.ReadUInt32();
				int diff = txtString.Text.Length - _stringsOriginal.Length;
				if (diff == 0)  //"express lane", if complete rewrite isn't needed
				{
					fs.Position = offset;
					bw.Write(txtString.Text.ToCharArray()); // null-term already there
					fs.Close();
					return;
				}
				for (; fs.Position < 0xadc;)     // update offsets
				{
					int off = br.ReadInt32() + diff;
					fs.Position -= 4;    // go back
					bw.Write(off);
				}
				fs.Position = offset + _stringsOriginal.Length + 1;  // Position to next string
				byte[] big = new byte[fs.Length - fs.Position];
				big = br.ReadBytes(big.Length); // read rest of the file
				fs.Position = offset;
				bw.Write(txtString.Text.ToCharArray()); // write the string
				fs.WriteByte(0);
				bw.Write(big);  // write the rest of the file
				fs.SetLength(fs.Position);
			}
		}
		
		void readStrings()
		{
			using (FileStream fs = File.Open(_filePath + "\\STRINGS.DAT", FileMode.Open, FileAccess.ReadWrite))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					fs.Position = _activeString * 4;     //Position to previous offset declaration
					int SPos = br.ReadInt32();
					int len;
					if (_activeString != 694) len = (int)(br.ReadUInt32() - SPos - 1);
					else len = (int)(fs.Length - SPos - 1);
					fs.Position = SPos;
					_stringsOriginal = new string(br.ReadChars(len));
					lblSPos.Text = (_activeString + 1) + " / 695";      //Update position label
					txtString.Text = _stringsOriginal;                  //Update Text box
				}
			}
		}
		#region Strings nav buttons
		void cmdSPrevClick(object sender, EventArgs e)
		{	//Prev/Next are by 1
			updateStrings();
			if (_activeString != 0) _activeString--;
			readStrings();
		}
		void cmdSNextClick(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString != 694) _activeString++;
			readStrings();
		}		
		void cmdSPrev2Click(object sender, EventArgs e)
		{	//Prev2/Next2 are by 20
			updateStrings();
			if (_activeString < 20) _activeString = 0; else _activeString -= 20;
			readStrings();
		}
		void cmdSNext2Click(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString > 674) _activeString = 694; else _activeString += 20;
			readStrings();
		}
		void cmdSPrev3Click(object sender, EventArgs e)
		{	//Prev3/Next3 are by 100
			updateStrings();
			if (_activeString < 100) _activeString = 0; else _activeString -= 100;
			readStrings();
		}
		void cmdSNext3Click(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString > 594) _activeString = 694; else _activeString += 100;
			readStrings();
		}
		#endregion		

		void loadTieText()
		{
			using (FileStream fsTieText = File.Open(_filePath + "\\Resource\\TieText" + _currentTieTextFile + ".lfd", FileMode.Open, FileAccess.ReadWrite))
			{
				_activeTieText = 0;
				var rmap = new Rmap(fsTieText);
				_tieTextOffset = rmap.SubHeaders[_currentTieTextFile == 0 ? 1 : 0].Offset;
				_text = new Text(fsTieText, _tieTextOffset);
			}
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
		#region TieText nav buttons
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
		#region Shipset buttons
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
    }
}