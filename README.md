# libs.loghelper

A library to create logs in C # simply and quickly. This library was inspired by the "cat" system of the Android environment.

This library uses threads to write log data to text files which means that calls do not degrade the performance of regular applications

This library controls the archiving of old logs. By default, this is done on the first operation of each month, for example, every first log operation of each month will archive the corresponding log.
LogHelper will also look for the 7zip program in your program's root folder or verify that you are on a UNIX system. If it finds any of them, the log file will also be compressed.


# Usage example

```C#
    Using namespace Shared;
    ...
    //creates the LogHelper instance
    Log log = new Log("DefaultMachine");
    log.log("this is a text");
    ...
    log.log("The Log.log method can receive other types, instead of strings, like this number: ", 1.5f, "or this Jsonmaker.JSON object: ", new JSON("{\"name\":\"Joseph\"}"));
    //in case of the objects, the LogHelper will call the method "ToString" of them.
```

# Where are my logs?

By default, logs are stored in .log files inside a folder named "logs" in your program root directory. 
If you need, you can change this directory during object instanciation:

```c#
    Log log = new Log("Name of my log file", ArchiveType.mounthly, "log", "/a/directory/path/where/logs/will/be/stored");
```
