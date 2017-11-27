# GAEM-PseudoETWToNeo4jImport
A tool to transform the pseudonymized etw and doxygen data into a format that the neo4j batch import tool can work with.

## Code quality
This is a prototype for research purposes, it's not pretty, it not robust and it's not flexible. However it works well enough for the purpose for which it was intended.

## Requirements
- Logs transformed by the GAEM-DataTransformationTool.
- Doxygen XML output transformed by the GAEM-DataTransformationTool.
- Neo4j 3.2 standalone installation (so on windows not the installer, but the zip file)
- Java Runtime Environment (preferably 64bit version)

## Usage
Make sure you have enough storage space, expect ~2 times the data size in eventual database size.

First run the exe
```cmd
>GAEM-DataTransformationTool.exe
```

No xml settings file can be provided as of yet, all settings are hardcoded at this time.

Next set environment variables
- JAVA_OPTS -d64 

	forces to use the 64 bit JVM machine, most pc's default to the 32 bit version due to being installed first for browsers and such

- HEAP_SIZE 24G 

	java heapsize used by the neo4j batch import tool, can also be achieved by setting JAVA_OPTS -Xmx24G (max heap size) -Xms24G (initial heap size, but this is then used for all java programs, ps. leave some memory for the system

Edit `neo4j-batch-import-script/import.bat` to suit yout needs and run the bat script, this can take quite a while depending on the size of the data set.
The database can be found in `<neo4j-zip-install-folder>/data/databases/<your databasename>`.
Next run the cypher queries in `neo4j-batch-import-script/index-queries.txt` manually (for example through the web interface), can take quite a while with large data sets!

## People
*empty for now, communicating who wants/should be here*

## License
[ISC](LICENSE)
