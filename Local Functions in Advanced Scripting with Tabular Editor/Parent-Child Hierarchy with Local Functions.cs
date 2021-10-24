// configuration start

int levels = 4;
int previouslevels = 4;

string tableName = "Entity";
string pathName = "EntityPath";
string keyName = "EntityKey";
string nameName = "EntityName";
string parentKeyName = "ParentEntityKey";
string levelNameFormat = "Level{0}";
string depthName = "Depth";
string rowDepthMeasureName = "EntityRowDepth";
string browseDepthMeasureName = "EntityBrowseDepth";
string wrapperMeasuresTableName = "StrategyPlan";
string hierarchyName = "Entities";
var wrapperMeasuresTable = Model.Tables[wrapperMeasuresTableName];
SortedDictionary<string, string> measuresToWrap = 
    new SortedDictionary<string, string> 
{ 
    { "# Categories", "# Categories Base"}, 
    { "Sum Amount", "Total Base" } 
};
// configuration end

// local functions

void DeleteHierarchy(string hierarchyName, string tableName)
{
    var table = Model.Tables[tableName];
    var hierarchiesCollection = table.Hierarchies.Where( 
        m => m.Name == hierarchyName );
    if (hierarchiesCollection.Count() > 0)
    {
        hierarchiesCollection.First().Delete();
    }
}

void DeleteMeasure(string measureName, string tableName)
{
    var table = Model.Tables[tableName];
    var measuresCollection = table.Measures.Where( 
        m => m.Name == measureName ); 
    if (measuresCollection.Count() > 0)
    {
        measuresCollection.First().Delete();
    }
} 


void DeleteCalculatedColumn(string calculatedColumnName, string tableName)
{
    var table = Model.Tables[tableName];
    var calculatedColumnsCollection = table.CalculatedColumns.Where( 
        m => m.Name == calculatedColumnName );
    if (calculatedColumnsCollection.Count() > 0)
    {   
        calculatedColumnsCollection.First().Delete();
    }
}    

// cleanup
DeleteHierarchy(hierarchyName, tableName);
foreach (var wrapperMeasurePair in measuresToWrap)
{
    DeleteMeasure(wrapperMeasurePair.Value, wrapperMeasuresTableName);
} 
DeleteMeasure(browseDepthMeasureName, tableName);
DeleteMeasure(rowDepthMeasureName, tableName);
DeleteCalculatedColumn(depthName, tableName);
for (int i = 1; i <= previouslevels; ++i)
{
    string levelName = string.Format(levelNameFormat, i);
    DeleteCalculatedColumn(levelName, tableName);
}
DeleteCalculatedColumn(pathName, tableName);

// create calculated columns
string daxLevelFormat = 
@"VAR LevelNumber = {0}
VAR LevelKey = PATHITEM( {1}[{2}], LevelNumber, INTEGER )
VAR LevelName = LOOKUPVALUE( {1}[{3}], {1}[{4}], LevelKey )
VAR Result = LevelName
RETURN
    Result
";

string daxPath = string.Format( "PATH({0}[{1}], {0}[{2}])", 
    tableName, keyName, parentKeyName);
var table = Model.Tables[tableName];
table.AddCalculatedColumn(pathName, daxPath);

for (int i = 1; i <= levels; ++i)
{
    string levelName = string.Format(levelNameFormat, i);
    string daxLevel = string.Format(daxLevelFormat, i, 
        tableName, pathName, nameName, keyName);
    table.AddCalculatedColumn(levelName, daxLevel);
}

string daxDepthFormat = "PATHLENGTH( {0}[{1}] )";
string daxDepth = string.Format(
    daxDepthFormat, tableName, pathName); 
table.AddCalculatedColumn(depthName, daxDepth);


// Create Hierarchy

table.AddHierarchy(hierarchyName);
for (int i = 1; i <= levels; ++i)
{
    string levelName = string.Format(levelNameFormat, i);
    string daxLevel = string.Format(daxLevelFormat, i, 
        tableName, pathName, nameName, keyName);
    table.Hierarchies[hierarchyName].AddLevel(levelName);
}

// Create measures
string daxRowDepthMeasureFormat = "MAX( {0}[{1}])";
string daxRowDepthMeasure = string.Format(
    daxRowDepthMeasureFormat, tableName, depthName );
table.AddMeasure(rowDepthMeasureName, daxRowDepthMeasure);

string daxBrowseDepthMeasure = "";
for (int i = 1; i <= levels; ++i)
{
    string levelMeasureFormat = "ISINSCOPE( {0}[{1}] )";
    string levelName = string.Format(levelNameFormat, i);
    daxBrowseDepthMeasure += string.Format(
        levelMeasureFormat, tableName, levelName);
    if (i < levels)
    {
        daxBrowseDepthMeasure += " + ";
    }
}
table.AddMeasure(browseDepthMeasureName, daxBrowseDepthMeasure);

string daxWrapperMeasureFormat = 
@"VAR Val = [{0}]
VAR ShowRow = [{1}] <= [{2}]
VAR Result = IF( ShowRow, Val )
RETURN
    Result
";

foreach (var wrapperMeasurePair in measuresToWrap)
{
    string daxWrapperMeasure = string.Format(daxWrapperMeasureFormat,
        wrapperMeasurePair.Key, // measure to be wrapped
        browseDepthMeasureName,
        rowDepthMeasureName);
    wrapperMeasuresTable.AddMeasure(wrapperMeasurePair.Value, daxWrapperMeasure);
    
} 

table.Measures.FormatDax(false);
wrapperMeasuresTable.Measures.FormatDax(false);

