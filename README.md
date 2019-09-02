# RPLidar.NET

This a .NET Standard 2.0 library written in C# to interface with Slamtech RPLidar. Tested with model A1 only.

I took Python RPLidar implementation as a reference:
https://github.com/Roboticia/RPLidar

At the moment it supports commands:
 - Get info
 - Get health
 - Get configuration
 - Control motor via DTR signal
 - Start legacy scan 
 - Start express legacy scan
 - Stop scan
 - Reset
 
All functions are blocking, except scan and measurements fetching functions which just get as much data as are in SerialPort buffer.