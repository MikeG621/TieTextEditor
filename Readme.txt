TIE Text Editor (formerly TIE File Reader)
======================================

Author: Michael Gaisser (mjgaisser@gmail.com)
Version: 1.2
Date: 2022.07.30

This utility enables the editing of strings for TIE95. Good for changing craft
names, tech room info, other strings in the game.

==Install==
TTE does not use an installer, simply download the latest ZIP file from the
"Releases" page on Github and extract to your location of choice.

==Uninstall==
To remove TTE,  simply delete the directory where you placed it. There will be
a "Settings.ini" located at
"C:\Users\<user>\AppData\Local\Imperial Department of Military Research\TTE"
which only saves the location of the TIE install so you don't have to select it
every time.

==========
Version History

v1.2, xx xxx 2022
- Converted from legacy code to modern project style
- Renamed to Tie Text Editor
- Added MPL

v1.1.1, 02 Dec 2008
- Minor code changes

v1.1, 31 Oct 2008
- Redid a lot of code, no longer keeps file open the entire time program is running.

v1.0, 11 Nov 2007
- Release

==========
Usage Notes

The number at the bottom of the program is a counter keeping track of the
difference between the original numbr of characters and what you have
currently. It is *highly* encouraged that you keep that number zero.  The
program does compensate if you decided to make it otherwise, but note that this
may prevent compatability with other patches and may have other unforseen
consequqnces.

Single arrows move 1 string. Double move 20 in STRINGS.DAT, 5 in the others.
Triple arrows move 100 in STRINGS.DAT, 20 in TieText0.lfd, and 10 in
Shipset#.lfd. For Shipset#.lfd, the File buttons change which file you're
editing, there's two.

The manual selection of the TIE installation and how it's saved is very crude
for the time being and will absolutely be reworked with full auto-detection at
a later point. After 1.1.1 is was simply a hard-coded location for my own use.
If the wrong directory is loaded, the program will crash. Start it again to
select a different directory.

If a good directory is selected, you'll be able to flip through the current
files. If for some reason that directory no longer becomes available, it will
crash and you'll need to restart like you've never run it. Like I said, pretty
crude right now.

The current state is just to get it out there again.

==========
Copyright Information

Copyright © 2007- Michael Gaisser
This program and related files are licensed under the Mozilla Public License.
See License.txt for the full text. If for some reason License.txt was not
distributed with this program, you can obtain the full text of the license at
http://mozilla.org/MPL/2.0/.

The Galactic Empire: Empire Reborn is Copyright © 2004- Tiberius Fel

"Star Wars" and related items are trademarks of LucasFilm Ltd and
LucasArts Entertainment Co.

This software is provided "as is" without warranty of any kind; including that
the software is free of defects, merchantable, fit for a particular purpose or
non-infringing. See the full license text for more details.