/*
 * TieTextEditor.exe, Allows the editing of TEXT resources and STRINGS.DAT from TIE
 * Copyright (C) 2006-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * VERSION: 1.2+
 */

/* CHANGELOG
 * v1.2, 220824
 * [UPD] Updated from legacy
 */

using Idmr.LfdReader;
using System;
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
		string[] _shipsetStrings;
        // other
        readonly string _filePath;

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
			_activeString = 1;		//start at 1
			readStrings();
			//TieText--------
			loadTieText();
			//Shipset1-------
			FileStream fsShip = File.Open(_filePath + "\\Resource\\Shipset1.lfd", FileMode.Open, FileAccess.ReadWrite);
			_activeShipset = 1;
			Text texSh = new Text(fsShip, Resource.HeaderLength * 2);		// only resource in RMAP, no need to check
			fsShip.Close();
			_shipsetStrings = texSh.Strings;
			readShipset();
		}

		void updateStrings()    //writes new string to file, and updates file if neccessary
		{
			if (txtString.Text == _stringsOriginal) return; //ignore if no changes

            //begin rewrite section
            FileStream fsStrings = File.Open(_filePath + "\\STRINGS.DAT", FileMode.Open, FileAccess.ReadWrite);
			BinaryReader br = new BinaryReader(fsStrings);
			BinaryWriter bw = new BinaryWriter(fsStrings);
			fsStrings.Position = (_activeString - 1) * 4;
			uint offset = br.ReadUInt32();        //String offset
            int diff = txtString.Text.Length - _stringsOriginal.Length;
            if (diff == 0)  //"express lane", if complete rewrite isn't needed
			{
				fsStrings.Position = offset;          //Position to string beginning
				bw.Write(txtString.Text.ToCharArray()); // null-term already there
				fsStrings.Close();
				return;
			}
			for(;fsStrings.Position<0xadc;)		// update offsets
			{
				int off = br.ReadInt32() + diff;
				fsStrings.Position -= 4;	// go back
				bw.Write(off);
			}
			fsStrings.Position = offset + _stringsOriginal.Length + 1;	// Position to next string
			byte[] Big = new byte[fsStrings.Length - fsStrings.Position];
			Big = br.ReadBytes(Big.Length);	// read rest of the file
			fsStrings.Position = offset;
			bw.Write(txtString.Text.ToCharArray());	// write the string
			fsStrings.WriteByte(0);
			bw.Write(Big);	// write the rest of the file
			fsStrings.SetLength(fsStrings.Position);
			fsStrings.Close();
		}
		
		void readStrings()
		{
			FileStream fsStrings = File.Open(_filePath + "\\STRINGS.DAT", FileMode.Open, FileAccess.ReadWrite);;
			BinaryReader br = new BinaryReader(fsStrings);
			fsStrings.Position = (_activeString - 1) * 4;		//Position to previous offset declaration
			int SPos = br.ReadInt32();
			int len;
			if (_activeString != 695) len = (int)(br.ReadUInt32() - SPos - 1);
			else len = (int)(fsStrings.Length - SPos - 1);
			fsStrings.Position = SPos;
			_stringsOriginal = new string(br.ReadChars(len));
			lblSPos.Text = _activeString + " / 695";		//Update position label
			txtString.Text = _stringsOriginal;					//Update Text box
			fsStrings.Close();
		}
		#region Strings nav buttons
		void cmdSPrevClick(object sender, EventArgs e)
		{		//Prev/Next are by 1
			updateStrings();	//redetermine offsets for every string, incase of length change
			if (_activeString != 1) _activeString--;
			readStrings();
		}
		void cmdSNextClick(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString != 695) _activeString++;
			readStrings();
		}		
		void cmdSPrev2Click(object sender, EventArgs e)
		{		//Prev2/Next2 are by 20
			updateStrings();
			if (_activeString <= 20) _activeString = 1; else _activeString -= 20;
			readStrings();
		}
		void cmdSNext2Click(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString > 675) _activeString = 695; else _activeString += 20;
			readStrings();
		}
		void cmdSPrev3Click(object sender, EventArgs e)
		{		//Prev3/Next3 are by 100
			updateStrings();
			if (_activeString <= 100) _activeString = 1; else _activeString -= 100;
			readStrings();
		}
		void cmdSNext3Click(object sender, EventArgs e)
		{
			updateStrings();
			if (_activeString > 595) _activeString = 695; else _activeString += 100;
			readStrings();
		}
		#endregion		

		void loadTieText()
		{
            FileStream fsTieText = File.Open(_filePath + "\\Resource\\TieText" + _currentTieTextFile + ".lfd", FileMode.Open, FileAccess.ReadWrite);
            _activeTieText = 0;
            Rmap rmap = new Rmap(fsTieText);
            _tieTextOffset = rmap.SubHeaders[_currentTieTextFile == 0 ? 1 : 0].Offset;
            _text = new Text(fsTieText, _tieTextOffset);
            fsTieText.Close();
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
			if (_activeTieText < _tieTextSubstrings.Length - 1) _activeTieText++;
			readTieText();
		}
		void cmdTPrevClick(object sender, EventArgs e)
		{
			updateTieText();
			if (_activeTieText > 0) _activeTieText--;
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
			if (_currentTTArray + 1 == _text.NumberOfStrings) return;
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
		void txtNameTextChanged(object sender, EventArgs e)
		{
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtOPTTextChanged(object sender, EventArgs e)
		{
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine1TextChanged(object sender, EventArgs e)
		{
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine2TextChanged(object sender, EventArgs e)
		{
			if (txtLine2.Text != "") txtLine3.Enabled = true; else txtLine3.Enabled = false;
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine3TextChanged(object sender, EventArgs e)
		{
			if (txtLine3.Text != "") txtLine4.Enabled = true; else txtLine4.Enabled = false;
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine4TextChanged(object sender, EventArgs e)
		{
			if (txtLine4.Text != "") txtLine5.Enabled = true; else txtLine5.Enabled = false;
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine5TextChanged(object sender, EventArgs e)
		{
			if (txtLine5.Text != "") txtLine6.Enabled = true; else txtLine6.Enabled = false;
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void txtLine6TextChanged(object sender, EventArgs e)
		{
			int Count;
			Count = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Count++;
			if (txtLine3.Text != "") Count++;
			if (txtLine4.Text != "") Count++;
			if (txtLine5.Text != "") Count++;
			if (txtLine6.Text != "") Count++;
			lblShCount.Text = Count.ToString();
		}
		void lblShCountTextChanged(object sender, EventArgs e)
		{
			if(lblShCount.Text == "0") lblShCount.ForeColor = System.Drawing.Color.Lime;
			else lblShCount.ForeColor = System.Drawing.Color.Red;
		}
		void lblSCountTextChanged(object sender, EventArgs e)
		{
			if(lblSCount.Text == "0") lblSCount.ForeColor = System.Drawing.Color.Lime;
			else lblSCount.ForeColor = System.Drawing.Color.Red;
		}
		void lblTCountTextChanged(object sender, EventArgs e)
		{
			if(lblTCount.Text == "0") lblTCount.ForeColor = System.Drawing.Color.Lime;
			else lblTCount.ForeColor = System.Drawing.Color.Red;
		}
		#endregion
		
		void updateShipset()
		{
			//TODO: overhaul
			string[] substrings = _shipsetStrings[_activeShipset-1].Split('\0');
			bool lines = true;
			if (substrings.Length > 2 && txtLine1.Text != substrings[2]) lines = false;
			if (substrings.Length > 3 && txtLine2.Text != substrings[3]) lines = false;
			if (substrings.Length > 4 && txtLine3.Text != substrings[4]) lines = false;
			if (substrings.Length > 5 && txtLine4.Text != substrings[5]) lines = false;
			if (substrings.Length > 6 && txtLine5.Text != substrings[6]) lines = false;
			if (substrings.Length > 7 && txtLine6.Text != substrings[7]) lines = false;
			if(txtName.Text == substrings[0] && txtOPT.Text == substrings[1] && lines) return;
			int Diff;
			Diff = txtName.Text.Length + txtOPT.Text.Length + txtLine1.Text.Length + txtLine2.Text.Length + 
				txtLine3.Text.Length + txtLine4.Text.Length + txtLine5.Text.Length + txtLine6.Text.Length - _shipsetStrings[_activeShipset-1].Length + 4;
			if (txtLine2.Text != "") Diff++;
			if (txtLine3.Text != "") Diff++;
			if (txtLine4.Text != "") Diff++;
			if (txtLine5.Text != "") Diff++;
			if (txtLine6.Text != "") Diff++;
			FileStream stream = File.Open(_filePath + "\\Resource\\" + tabShipset.Text, FileMode.Open, FileAccess.ReadWrite);
			Text texSh = new Text(stream,Resource.HeaderLength*2);
			BinaryReader br = new BinaryReader(stream);
			BinaryWriter bw = new BinaryWriter(stream);
			long shipsetPosition = texSh.Offset + 0x12 + (_activeShipset-1)*2;
			for (int i=0;i<_activeShipset-1;i++) shipsetPosition += _shipsetStrings[i].Length;
			#region without total rewrite, just entry
			if (Diff == 0)
			{
				stream.Position = shipsetPosition + 2;
				bw.Write(txtName.Text.ToCharArray());
				stream.WriteByte(0);
				bw.Write(txtOPT.Text.ToCharArray());
				stream.WriteByte(0);
				if (txtLine1.Text != "")
				{
					bw.Write(txtLine1.Text.ToCharArray());
					stream.WriteByte(0);
				}
				if (txtLine2.Text != "")
				{
					bw.Write(txtLine2.Text.ToCharArray());
					stream.WriteByte(0);
				}
				if (txtLine3.Text != "")
				{
					bw.Write(txtLine3.Text.ToCharArray());
					stream.WriteByte(0);
				}
				if (txtLine4.Text != "")
				{
					bw.Write(txtLine4.Text.ToCharArray());
					stream.WriteByte(0);
				}
				if (txtLine5.Text != "")
				{
					bw.Write(txtLine5.Text.ToCharArray());
					stream.WriteByte(0);
				}
				if (txtLine6.Text != "")
				{
					bw.Write(txtLine6.Text.ToCharArray());
					stream.WriteByte(0);
				}
				texSh = new Text(stream,Resource.HeaderLength*2);
				_shipsetStrings = texSh.Strings;
				stream.Close();
				return;
			}
			#endregion

			//thus begins the longest rewrite section...
			uint Diff2 = (uint)Diff;
			stream.Position = Resource.HeaderLength + Resource.LengthOffset;
			Diff2 += br.ReadUInt32();
			stream.Position -= 4;
			bw.Write(Diff2);	// Text length (RMAP)
			stream.Position = Resource.HeaderLength*2 + Resource.LengthOffset;
			bw.Write(Diff2);	// Text length
			//Diff2 no longer used
			stream.Position = shipsetPosition;
			bw.Write((ushort)(_shipsetStrings[_activeShipset-1].Length + Diff));	// string length
			stream.Position += _shipsetStrings[_activeShipset-1].Length;
			byte[] Big = new byte[stream.Length - shipsetPosition];
			Big = br.ReadBytes(Big.Length);	// read rest of the file
			stream.Position = shipsetPosition + 2;
			#region write current
			bw.Write(txtName.Text.ToCharArray());
			stream.WriteByte(0);
			bw.Write(txtOPT.Text.ToCharArray());
			stream.WriteByte(0);
			if (txtLine1.Text != "")
			{
				bw.Write(txtLine1.Text.ToCharArray());
				stream.WriteByte(0);
			}
			if (txtLine2.Text != "")
			{
				bw.Write(txtLine2.Text.ToCharArray());
				stream.WriteByte(0);
			}
			if (txtLine3.Text != "")
			{
				bw.Write(txtLine3.Text.ToCharArray());
				stream.WriteByte(0);
			}
			if (txtLine4.Text != "")
			{
				bw.Write(txtLine4.Text.ToCharArray());
				stream.WriteByte(0);
			}
			if (txtLine5.Text != "")
			{
				bw.Write(txtLine5.Text.ToCharArray());
				stream.WriteByte(0);
			}
			if (txtLine6.Text != "")
			{
				bw.Write(txtLine6.Text.ToCharArray());
				stream.WriteByte(0);
			}
			stream.WriteByte(0);
			#endregion
			bw.Write(Big);			//write the sucker in
			stream.SetLength(stream.Position);
			texSh = new Text(stream,Resource.HeaderLength*2);
			_shipsetStrings = texSh.Strings;
			stream.Close();
		}
		
		void readShipset()
		{
			string[] substrings = _shipsetStrings[_activeShipset-1].Split('\0');
			txtName.Text = substrings[0];
			txtOPT.Text = substrings[1];
			txtLine1.Text = ""; txtLine2.Text = ""; txtLine3.Text = ""; txtLine4.Text = ""; txtLine5.Text = ""; txtLine6.Text = "";
			if (substrings.Length > 2) txtLine1.Text = substrings[2];
			if (substrings.Length > 3) txtLine2.Text = substrings[3];
			if (substrings.Length > 4) txtLine3.Text = substrings[4];
			if (substrings.Length > 5) txtLine4.Text = substrings[5];
			if (substrings.Length > 6) txtLine5.Text = substrings[6];
			if (substrings.Length > 7) txtLine6.Text = substrings[7];
			lblShPos.Text = _activeShipset.ToString() + " / " + _shipsetStrings.Length.ToString();
		}
		#region Shipset buttons
		void cmdShNextClick(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset != _shipsetStrings.Length) _activeShipset++;
			readShipset();
		}
		void cmdShPrevClick(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset != 1) _activeShipset--;
			readShipset();
		}
		void cmdShNext2Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset > (_shipsetStrings.Length - 5)) _activeShipset = _shipsetStrings.Length; else _activeShipset += 5;
			readShipset();
		}
		void cmdShPrev2Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset < 6) _activeShipset = 1; else _activeShipset -= 5;
			readShipset();
		}
		void cmdShNext3Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset > (_shipsetStrings.Length-10)) _activeShipset = _shipsetStrings.Length; else _activeShipset += 10;
			readShipset();
		}
		void cmdShPrev3Click(object sender, EventArgs e)
		{
			updateShipset();
			if (_activeShipset < 11) _activeShipset = 1; else _activeShipset -= 10;
			readShipset();
		}
		void cmdFileNextClick(object sender, EventArgs e)
		{
			//Shipset2
			updateShipset();
			FileStream fsShip = File.Open(_filePath + "\\Resource\\Shipset2.lfd", FileMode.Open, FileAccess.ReadWrite);
			_activeShipset = 1;
			Text texSh = new Text(fsShip,0x20);
			fsShip.Close();
			_shipsetStrings = texSh.Strings;
			readShipset();
			cmdFileNext.Enabled = false;
			cmdFilePrev.Enabled = true;
			tabShipset.Text = "Shipset2.lfd";
		}
		void cmdFilePrevClick(object sender, EventArgs e)
		{
			//Shipset1
			updateShipset();
			FileStream fsShip = File.Open(_filePath + "\\Resource\\Shipset1.lfd", FileMode.Open, FileAccess.ReadWrite);
			_activeShipset = 1;
			Text texSh = new Text(fsShip,0x20);
			fsShip.Close();
			_shipsetStrings = texSh.Strings;
			readShipset();
			cmdFileNext.Enabled = true;
			cmdFilePrev.Enabled = false;
			tabShipset.Text = "Shipset1.lfd";
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
    }
}