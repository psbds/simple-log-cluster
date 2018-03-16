# simple-log-cluster

### Required Settings
*  .NET Core https://www.microsoft.com/net/download

### Input File Schema
*  One example per line.
    

### How to run it

1. Extract the content and enter the project folder
3. Run ```dotnet restore && dotnet build```
4. Run ```dotnet run THRESHOLD INPUT_PATH OUTPUT_PATH ```. Example: ```dotnet run 0.9 "D:\input.csv" "D:\output.csv"```
