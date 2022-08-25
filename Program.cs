/*
 * TieTextEditor.exe, Allows the editing of TEXT resources and STRINGS.DAT from TIE
 * Copyright (C) 2006-2022 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * VERSION: 1.2
 */

/* CHANGELOG
 * v1.2, 220824
 * [UPD] Updated from legacy
 */

using System;
using System.Windows.Forms;

namespace Idmr.TieTextEditor
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
