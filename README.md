## iKGD
----
A fully automated tool to grab AES keys for iOS firmwares. iKGD takes about 25 seconds to extract the firmware, parse stuff we need, exploit the device and run iKGD cyanide payload, grab keys, make The iPhone Wiki keys file and opensn0w plist.

### Usage

	iKGD.exe <args> [options]
	  -i <ipswlocation>     Local path to the iOS firmware
	  -u <ipswurl>          Remote firmware URL to download files from
	  -d <device>           Device boardid as n90ap to fetch URL (use with -f)
	  -f <firmwarebuild>    Firmware build as 9A334 to fetch URL (use with -d)
	  -k <keysdir>          Path to dir to store keys (default "C:\IPSW\Keys\")
	  -e                    Extract full root filesystem (only with -i)
	  -r                    Don't reboot device.
	  -v                    Verbose

+ Use `-i` if you have the firmware locally.
+ Use `-u` if you have a link to the firmware you want to download files from. (about 20-25MB)
+ Use `-d` and `-f` and fetch the url from Firmware Links API and continue what `-u` would do.

### Credits

* [cyanide](https://github.com/Chronic-Dev/cyanide)
* [icj.me](http://api.ios.icj.me/v2)
* [iH8sn0w](http://ih8sn0w.com/)
* [irecovery](https://github.com/Chronic-Dev/libirecovery)
* [PlistCS](https://github.com/animetrics/PlistCS)
* RemoteZipFile - Emanuele Ruffaldi
* [SharpZipLib](http://sharpziplib.com/)
* [syringe](https://github.com/Chronic-Dev/syringe)
* XGetopt - Hans Dietrich
* [Xpwn](https://github.com/planetbeing/xpwn)

### License

	Copyright (c) 2012 Neal (neal@ineal.me)

	Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
	associated documentation files (the "Software"), to deal in the Software without restriction, including 
	without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
	sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
	subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all copies or substantial 
	portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
	LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
	WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
	SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

