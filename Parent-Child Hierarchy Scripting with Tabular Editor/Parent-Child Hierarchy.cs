// configuration start

int levels = 4;

string tableName = "Entity";
var table = Model.Tables[tableName];

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

string daxPath = string.Format( "PATH({0}[{1}], {0}[{2}])", 
    tableName, keyName, parentKeyName);
    

// cleanup

var hierarchiesCollection = table.Hierarchies.Where( 
    m => m.Name == hierarchyName );
if (hierarchiesCollection.Count() > 0)
{
    hierarchiesCollection.First().Delete();
}


foreach (var wrapperMeasurePair in measuresToWrap)
{
    var wrapperMeasuresCollection = wrapperMeasuresTable.Measures.Where( 
        m => m.Name == wrapperMeasurePair.Value ); 
    if (wrapperMeasuresCollection.Count() > 0)
    {
        wrapperMeasuresCollection.First().Delete();
    }
} 

var browseDepthMeasureCollection = table.Measures.Where( 
    m => m.Name == browseDepthMeasureName );
if (browseDepthMeasureCollection.Count() > 0)
{
    browseDepthMeasureCollection.First().Delete();
}

var rowDepthMeasureCollection = table.Measures.Where( 
    m => m.Name == rowDepthMeasureName );
if (rowDepthMeasureCollection.Count() > 0)
{
    rowDepthMeasureCollection.First().Delete();
}

var depthCollection = table.CalculatedColumns.Where( 
    m => m.Name == depthName );
if (depthCollection.Count() > 0)
{   
    depthCollection.First().Delete();
}

for (int i = 1; i <= levels; ++i)
{
    string levelName = string.Format(levelNameFormat, i);
    var levelCalculatedColumnCollection = 
        table.CalculatedColumns.Where( m => m.Name == levelName );
    if (levelCalculatedColumnCollection.Count() > 0)
    {    
        levelCalculatedColumnCollection.First().Delete();
    }
}

var pathCalculatedColumnCollection = table.CalculatedColumns.Where( 
    m => m.Name == pathName );
if (pathCalculatedColumnCollection.Count() > 0)
{
    pathCalculatedColumnCollection.First().Delete();
}

// create calculated columns
table.AddCalculatedColumn(pathName, daxPath);

string daxLevelFormat = 
@"VAR LevelNumber = {0}
VAR LevelKey = PATHITEM( {1}[{2}], LevelNumber, INTEGER )
VAR LevelName = LOOKUPVALUE( {1}[{3}], {1}[{4}], LevelKey )
VAR Result = LevelName
RETURN
    Result
";

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