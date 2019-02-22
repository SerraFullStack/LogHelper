# libs.loghelper

A library to create logs in C # simply and quickly. This library was inspired by the "cat" system of the Android environment.

This library uses threads to write log data to text files which means that calls do not degrade the performance of regular applications

This library controls the archiving of old logs. By default, this is done in the first operation of each month, that is, every first log operation of each month will archive the corresponding log.
LogHelper will also look for the 7zip program in the root folder of your program, or check if you are on a UNIX system. If it finds it, it will compact the log file.
