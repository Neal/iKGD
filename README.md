## iKGD
----
A fully automated tool to grab AES keys for iOS firmwares. iKGD takes about 25 seconds to extract the firmware, parse stuff we need, exploit the device and run iKGD cyanide payload, grab keys, make The iPhone Wiki keys file and opensn0w plist.

### Usage

	-i <ipswlocation>     Local path to the iOS firmware");
	-u <ipswurl>          Remote firmware URL to download files from");
	-d <device>           Device boardid as n90ap to fetch URL (use with -f)");
	-f <firmwarebuild>    Firmware build as 9A334 to fetch URL (use with -d)");
	-k <keysdir>          Path to dir to store keys (default \"C:\\IPSW\\Keys\\\"");
	-e                    Extract full root filesystem (only with -i)");
	-r                    Don't reboot device.");
	-v                    Verbose");

Use `-i` if you have the firmware.
Use `-u` if you have a link to the firmware you want to download files from. (about 20-25MB)
Use `-d` and `-f` and fetch the url from Firmware Links API and continue what `-u` would do.

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

