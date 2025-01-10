# AppCompatCacheParser

## Command Line Interface

    AppCompatCache Parser version 1.4.4.0
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/AppCompatCacheParser
    
            c               The ControlSet to parse. Default is to extract all control sets.
            f               Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used
            t               Sorts last modified timestamps in descending order
    
            csv             Directory to save CSV formatted results to. Required
            csvf            File name to save CSV formatted results to. When present, overrides default name
    
            debug           Debug mode
            dt              The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options. Default is: yyyy-MM-dd HH:mm:ss
            nl              When true, ignore transaction log files for dirty hives. Default is FALSE
    
    Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2
              AppCompatCacheParser.exe --csv c:\temp --csvf results.csv
    
              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

## Documentation

AppCompatCache (ShimCache) parser. Supports Windows XP, Windows 7 (x86 and x64), Windows 8.x, Windows 10, and Windows 11.

[Introducing AppCompatCacheParser](https://binaryforay.blogspot.com/2015/05/introducing-appcompatcacheparser.html)

[AppCompatCacheParser v0.0.5.1 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0051-released.html)

[AppCompatCacheParser v0.0.5.2 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0052-released.html)

[AppCompatCacheParser v0.9.0.0 released and some AppCompatCache/shimcache parser testing](https://binaryforay.blogspot.com/2016/05/appcompatcacheparser-v0900-released-and.html)

[Windows 10 Creators update vs shimcache parsers: Fight!!](https://binaryforay.blogspot.com/2017/03/windows-10-creators-update-vs-shimcache.html)

[Updates to the left of me, updates to the right of me, version 1 releases are here (for the most part)](https://binaryforay.blogspot.com/2018/03/updates-to-left-of-me-updates-to-right.html)

[Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html)

[Locked file support added to AmcacheParser, AppCompatCacheParser, MFTECmd, ShellBags Explorer (and SBECmd), and Registry Explorer (and RECmd)](https://binaryforay.blogspot.com/2019/01/locked-file-support-added-to.html)

[Windows Registry Knowledge Base](https://github.com/libyal/winreg-kb/wiki/Application-Compatibility-Cache-key)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). 

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
