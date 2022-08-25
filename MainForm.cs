/*
 * TieTextEditor.exe, Allows the editing of TEXT resources and STRINGS.DAT from TIE
 * Copyright (C) 2006-2022 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * VERSION: 1.2
 */

/* CHANGELOG
 * v1.2, xxxxxx
 * [UPD] Updated from legacy
 */

using Idmr.LfdReader;
using System;
using System.IO;
using System.Windows.Forms;

namespace Idmr.TieTextEditor
{
	/// <summary>
	/// Reads and edits STRINGS.DAT, TieText0.lfd, and Shipset#.lfd
	/// 
	/// S buttons for navigating 1/20/100 strings at a time
	/// T buttons for 1/5/20
	/// Sh buttons for 1/5/10
	/// tabs seperating files
	/// TieText is only for ships
	/// 
	/// 'S' prefix denotes STRINGS.DAT
	/// 'T' prefix denotes TieText0.lfd
	/// 'Sh' prefix denotes Shipset#.lfd (all-in-one)
	/// </summary>
	public partial class MainForm : Form
	{
		// Strings
		int _activeString;
		string _stringsOriginal;
		// TieText
		int _activeTieText;
		string[] _tieTextSubstrings;
		long _tieTextOffset;
		// Shipset
		int _activeShipset;
		string[] _shipsetStrings;
		// other
		string _filePath;

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

			//_filePath = "E:\\Program Files (x86)\\Steam\\SteamApps\\common\\STAR WARS Tie Fighter\\remastered";
			//Strings--------
			_activeString = 1;		//start at 1
			readStrings();
			//TieText--------
			FileStream fsTieText = File.Open(_filePath + "\\Resource\\TieText0.lfd", FileMode.Open, FileAccess.ReadWrite);
			_activeTieText = 1;
			Rmap rmpTT = new Rmap(fsTieText);
			_tieTextOffset = rmpTT.SubHeaders[1].Offset;
			Text TextT = new Text(fsTieText, _tieTextOffset);
			_tieTextSubstrings = TextT.Strings[2].Split('\0');
			readTieText();
			fsTieText.Close();
			//Shipset1-------
			FileStream fsShip = File.Open(_filePath + "\\Resource\\Shipset1.lfd", FileMode.Open, FileAccess.ReadWrite);
			_activeShipset = 1;
			Text texSh = new Text(fsShip, Resource.HeaderLength * 2);		// only resource in RMAP, no need to check
			fsShip.Close();
			_shipsetStrings = texSh.Strings;
			readShipset();
		}

		void updateStrings()	//writes new string to file, and updates file if neccessary
		{
			if (txtString.Text == _stringsOriginal) return;	//ignore if no changes
			FileStream fsStrings = File.Open(_filePath + "\\STRINGS.DAT", FileMode.Open, FileAccess.ReadWrite);
			BinaryReader br = new BinaryReader(fsStrings);
			BinaryWriter bw = new BinaryWriter(fsStrings);
			uint Diff = (uint)(txtString.Text.Length - _stringsOriginal.Length);	//begin rewrite section
			int SROffset = (_activeString - 1) * 4;		//SRecord offset
			fsStrings.Position = SROffset;
			uint SOff = br.ReadUInt32();		//String offset
			if (Diff == 0)	//"express lane", if complete rewrite isn't needed
			{
				fsStrings.Position = SOff;			//Position to string beginning
				bw.Write(txtString.Text.ToCharArray());	// null-term already there
				fsStrings.Close();
				return;
			}
			DialogResult Warning;
			Warning = MessageBox.Show("WARNING! Changing string length may prevent compatability with other patches.\nDo you wish to continue?",
						"WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (Warning == DialogResult.No) { fsStrings.Close(); return; }
			for(;fsStrings.Position<0xadc;)		// update offsets
			{
				uint off = br.ReadUInt32() + Diff;
				fsStrings.Position -= 4;	// go back
				bw.Write(off);
			}
			fsStrings.Position = SOff + _stringsOriginal.Length + 1;	// Position to next string
			byte[] Big = new byte[fsStrings.Length - fsStrings.Position];
			Big = br.ReadBytes(Big.Length);	// read rest of the file
			fsStrings.Position = SOff;
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
		void cmdSPrevClick(object sender, System.EventArgs e)
		{		//Prev/Next are by 1
			updateStrings();	//redetermine offsets for every string, incase of length change
			if (_activeString != 1) _activeString--;
			readStrings();
		}
		void cmdSNextClick(object sender, System.EventArgs e)
		{
			updateStrings();
			if (_activeString != 695) _activeString++;
			readStrings();
		}		
		void cmdSPrev2Click(object sender, System.EventArgs e)
		{		//Prev2/Next2 are by 20
			updateStrings();
			if (_activeString <= 20) _activeString = 1; else _activeString -= 20;
			readStrings();
		}
		void cmdSNext2Click(object sender, System.EventArgs e)
		{
			updateStrings();
			if (_activeString > 675) _activeString = 695; else _activeString += 20;
			readStrings();
		}
		void cmdSPrev3Click(object sender, System.EventArgs e)
		{		//Prev3/Next3 are by 100
			updateStrings();
			if (_activeString <= 100) _activeString = 1; else _activeString -= 100;
			readStrings();
		}
		void cmdSNext3Click(object sender, System.EventArgs e)
		{
			updateStrings();
			if (_activeString > 595) _activeString = 695; else _activeString += 100;
			readStrings();
		}
		#endregion		
		
		void updateTieText()
		{
			if (txtTieText.Text == _tieTextSubstrings[_activeTieText-1]) { return; }
			FileStream fsTieText = File.Open(_filePath + "\\Resource\\TieText0.lfd", FileMode.Open, FileAccess.ReadWrite);
			BinaryReader br = new BinaryReader(fsTieText);
			BinaryWriter bw = new BinaryWriter(fsTieText);
			int Diff = (txtTieText.Text.Length - _tieTextSubstrings[_activeTieText-1].Length);
			long lPos;
			Text TextT = new Text(fsTieText,_tieTextOffset);
			lPos = TextT.Offset + TextT.Strings[0].Length + TextT.Strings[1].Length + 23 + _activeTieText;
			for(int k=0;k<_activeTieText-1;k++) lPos += _tieTextSubstrings[k].Length;
			if (Diff == 0)	//"express lane", if complete rewrite isn't needed
			{
				fsTieText.Position = lPos;			//Position to string beginning
				bw.Write(txtTieText.Text.ToCharArray());		//write, null-term already there
				TextT = new Text(fsTieText,_tieTextOffset);		// reads it again since properties are read-only
				_tieTextSubstrings = TextT.Strings[2].Split('\0');
				fsTieText.Close();
				return;
			}
			DialogResult Warning;
			Warning = MessageBox.Show("WARNING! Changing string length may prevent compatability with other patches.\nDo you wish to continue?", "WARNING",
				MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (Warning == DialogResult.No) { return; }
			//begin rewrite section
			uint tDiff;
			fsTieText.Position = 0x2C;							//length byte for tieText0
			tDiff = br.ReadUInt32();
			fsTieText.Position -= 4;
			bw.Write((uint)(Diff+tDiff));
			fsTieText.Position = _tieTextOffset + 0xC;						//location #2
			bw.Write((uint)(Diff+tDiff));
			fsTieText.Position = TextT.Offset + TextT.Strings[0].Length + TextT.Strings[1].Length + 0x16;	//length byte for Ships section
			tDiff = br.ReadUInt16();
			fsTieText.Position -= 2;
			bw.Write((ushort)(Diff+tDiff));
			byte[] Big = new byte[fsTieText.Length - lPos];
			fsTieText.Position = lPos + _tieTextSubstrings[_activeTieText-1].Length+1;	// start of next substring
			Big = br.ReadBytes(Big.Length);	// read rest of the file
			fsTieText.Position = lPos;
			bw.Write(txtTieText.Text.ToCharArray());
			fsTieText.WriteByte(0);
			bw.Write(Big);			//write the sucker in
			fsTieText.SetLength(fsTieText.Position);
			TextT = new Text(fsTieText,_tieTextOffset);		// reads it again since properties are read-only
			_tieTextSubstrings = TextT.Strings[2].Split('\0');
			fsTieText.Close();
		}
		
		void readTieText()
		{
			txtTieText.Text = _tieTextSubstrings[_activeTieText-1];
			lblTPos.Text = _activeTieText + " / 84";
		}
		#region TieText nav buttons
		void cmdTNextClick(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText != 84) _activeTieText++;
			readTieText();
		}
		void cmdTPrevClick(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText != 1) _activeTieText--;
			readTieText();
		}
		void cmdTNext2Click(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText > 79) _activeTieText = 84; else _activeTieText += 5;
			readTieText();
		}
		void cmdPrev2Click(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText < 6) _activeTieText = 1; _activeTieText -= 5;
			readTieText();
		}
		void cmdTNext3Click(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText > 64) _activeTieText = 84; else _activeTieText += 20;
			readTieText();
		}
		void cmdPrev3Click(object sender, System.EventArgs e)
		{
			updateTieText();
			if (_activeTieText < 21) _activeTieText = 1; else _activeTieText -= 20;
			readTieText();
		}
		#endregion
		
		#region length labels
		void txtStringTextChanged(object sender, System.EventArgs e)
		{
			lblSCount.Text = (txtString.Text.Length - _stringsOriginal.Length).ToString();
		}
		void txtTieTextTextChanged(object sender, System.EventArgs e)
		{
			lblTCount.Text = (txtTieText.Text.Length - _tieTextSubstrings[_activeTieText-1].Length).ToString();
		}
		void txtNameTextChanged(object sender, System.EventArgs e)
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
		void txtOPTTextChanged(object sender, System.EventArgs e)
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
		void txtLine1TextChanged(object sender, System.EventArgs e)
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
		void txtLine2TextChanged(object sender, System.EventArgs e)
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
		void txtLine3TextChanged(object sender, System.EventArgs e)
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
		void txtLine4TextChanged(object sender, System.EventArgs e)
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
		void txtLine5TextChanged(object sender, System.EventArgs e)
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
		void txtLine6TextChanged(object sender, System.EventArgs e)
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
		void lblShCountTextChanged(object sender, System.EventArgs e)
		{
			if(lblShCount.Text == "0") lblShCount.ForeColor = System.Drawing.Color.Lime;
			else lblShCount.ForeColor = System.Drawing.Color.Red;
		}
		void lblSCountTextChanged(object sender, System.EventArgs e)
		{
			if(lblSCount.Text == "0") lblSCount.ForeColor = System.Drawing.Color.Lime;
			else lblSCount.ForeColor = System.Drawing.Color.Red;
		}
		void lblTCountTextChanged(object sender, System.EventArgs e)
		{
			if(lblTCount.Text == "0") lblTCount.ForeColor = System.Drawing.Color.Lime;
			else lblTCount.ForeColor = System.Drawing.Color.Red;
		}
		#endregion
		
		void updateShipset()
		{
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
			DialogResult Warning;
			Warning = MessageBox.Show("WARNING! Changing string length may prevent compatability with other patches.\nDo you wish to continue?", "WARNING",
				MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (Warning == DialogResult.No) { return; }
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
		void cmdShNextClick(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset != _shipsetStrings.Length) _activeShipset++;
			readShipset();
		}
		void cmdShPrevClick(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset != 1) _activeShipset--;
			readShipset();
		}
		void cmdShNext2Click(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset > (_shipsetStrings.Length - 5)) _activeShipset = _shipsetStrings.Length; else _activeShipset += 5;
			readShipset();
		}
		void cmdShPrev2Click(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset < 6) _activeShipset = 1; else _activeShipset -= 5;
			readShipset();
		}
		void cmdShNext3Click(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset > (_shipsetStrings.Length-10)) _activeShipset = _shipsetStrings.Length; else _activeShipset += 10;
			readShipset();
		}
		void cmdShPrev3Click(object sender, System.EventArgs e)
		{
			updateShipset();
			if (_activeShipset < 11) _activeShipset = 1; else _activeShipset -= 10;
			readShipset();
		}
		void cmdFileNextClick(object sender, System.EventArgs e)
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
		void cmdFilePrevClick(object sender, System.EventArgs e)
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
		void cmdNewClick(object sender, System.EventArgs e)
		{
			// TODO: allow inserting new entries
		}
		void cmdDelClick(object sender, System.EventArgs e)
		{
			// TODO: allow deleting entries
		}
		#endregion
	}
}