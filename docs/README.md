<img align="right" src="img/gemstone-wide-600.png" alt="gemstone logo">

# IO
### GPA Gemstone Library

The Gemstone IO Library organizes all Gemstone functionality related to input and output (IO).

[![GitHub license](https://img.shields.io/github/license/gemstone/io?color=4CC61E)](https://github.com/gemstone/io/blob/master/LICENSE)
[![Build status](https://ci.appveyor.com/api/projects/status/iv4bx8r22amt5tbv?svg=true)](https://ci.appveyor.com/project/ritchiecarroll/io)
![CodeQL](https://github.com/gemstone/io/workflows/CodeQL/badge.svg)
[![NuGet](https://img.shields.io/nuget/vpre/Gemstone.IO)](https://www.nuget.org/packages/Gemstone.IO#readme-body-tab)

This library includes helpful io classes like the following:

* [SafeFileWatcher](https://gemstone.github.io/io/help/html/T_Gemstone_IO_SafeFileWatcher.htm):
  * Represents a wrapper around the native .NET [FileSystemWatcher](https://docs.microsoft.com/dotnet/api/system.io.filesystemwatcher) that avoids problems with dangling references when using a file watcher instance as a class member that never gets disposed.
* [ChecksumExtensions](https://gemstone.github.io/io/help/html/T_Gemstone_IO_Checksums_ChecksumExtensions_ChecksumExtensions.htm):
  * Defines extension functions related to computing various types of standard checksums.
* [FileBackedDictionary](https://gemstone.github.io/io/help/html/T_Gemstone_IO_Collections_FileBackedDictionary_2.htm):
  * Represents a lookup table of key/value pairs backed by a file, with very little memory overhead.
* [FrameImageParserBase](https://gemstone.github.io/io/help/html/T_Gemstone_IO_Parsing_FrameImageParserBase_2.htm):
  * Defines a base class for basic parsing functionality suitable for automating the parsing of a binary data stream represented as frames with common headers and returning the parsed data via an event.

Among others.

### Documentation
[Full Library Documentation](https://gemstone.github.io/io/help)
