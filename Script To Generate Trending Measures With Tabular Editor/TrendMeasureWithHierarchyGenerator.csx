string tableForMeasures = "Sales";

// Table to be uses for the Hierarchy
string hierarchyTable = "Date";

// Hierarchy levels from higher to lower granularity
// each level contains the list of the columns to be 
// considered for the ISINSCOPE condition
// the first one is used as X value for the calculation 
// and must be of numeric type
List<string> level1 = new List<string> { "Date" };
List<string> level2 = new List<string> { 
    "Calendar Year Month Number", 
    "Calendar Year Month", 
    "Calendar Year Month As Date" };
List<string> level3 = new List<string> { 
    "Calendar Year Quarter Number", 
    "Calendar Year Quarter", 
    "Calendar Year Quarter As Date" };
List<string> level4 = new List<string> { 
    "Calendar Year Number", 
    "Calendar Year", 
    "Calendar Year As Date" };

List<List<string>> levels = new List<List<string>> {
    level1, level2, level3, level4};
 

// the measures for which the Trend measure is to be generated
List<string> measures = new List<string> { 
    "Margin", "Margin %", "Sales Amount" };

string trendMeasureBaseFormat = @"
IF (
    NOT ISBLANK ( {0} ),
    SWITCH (
        TRUE,
        {1}
    )
)";


string trendMeasureHierarchyLevelFormat = @"
{0},
    VAR Tab =
        FILTER (
            CALCULATETABLE (
                SELECTCOLUMNS (
                    SUMMARIZE ( {1}, {2} ),
                    ""@X"", CONVERT ( {2}, DOUBLE ),
                    ""@Y"", CONVERT ( {3}, DOUBLE )
                ),
                ALLSELECTED ( {1} )
            ),
            NOT ISBLANK ( [@X] ) && NOT ISBLANK ( [@Y] )
        )
    VAR SX =
        SUMX ( Tab, [@X] )
    VAR SY =
        SUMX ( Tab, [@Y] )
    VAR SX2 =
        SUMX ( Tab, [@X] * [@X] )
    VAR SXY =
        SUMX ( Tab, [@X] * [@Y] )
    VAR N =
        COUNTROWS ( Tab )
    VAR Denominator = N * SX2 - SX * SX
    VAR Slope =
        DIVIDE ( N * SXY - SX * SY, Denominator )
    VAR Intercept =
        DIVIDE ( SY * SX2 - SX * SXY, Denominator )
    VAR V =
        Intercept
            + Slope * VALUES ( {2} )
    RETURN
        V";


// retrieve the object to define the measures to
var table  = Model.Tables[tableForMeasures];
var tableMeasures = table.Measures;
        
// iterating over the measure to be generated 
foreach(string measureName in measures)
{
    string trendMeasureName = measureName + " Trend";
    string measure = string.Format("[{0}]", measureName);
    
    // generating the body of the trend measure: the levels of the hierarchy
    string body = "";
    for (int indexLevel = 0; indexLevel < levels.Count; ++indexLevel)
    {
        List<string> columns = levels[indexLevel];
        // generating the condition
        string xAxisColumn = string.Format(
            "'{0}'[{1}]", hierarchyTable, columns[0] );
        string condition = string.Format(
            "ISINSCOPE({0})", xAxisColumn);
        for (int indexColumn = 1; 
             indexColumn < columns.Count; 
             ++indexColumn)
        {
            string column = string.Format(
                "'{0}'[{1}]", hierarchyTable, 
                columns[indexColumn] );
            string conditionToAppend = string.Format(
                " || ISINSCOPE({0})", column);
            condition += conditionToAppend;
        }
        string bodyLevel = string.Format(
            trendMeasureHierarchyLevelFormat, 
            condition, hierarchyTable, xAxisColumn, measure ); 
        body += bodyLevel;
        if (indexLevel + 1 < levels.Count)
        {
            body += ",\n";
        }
    }
    
    // put the body into the trend measure base
    string trendMeasureDax = string.Format( 
        trendMeasureBaseFormat, measure, body );
    
    // checks if the measure exists, if not, creates it
    if (!tableMeasures.Contains(trendMeasureName))
    {
        table.AddMeasure(trendMeasureName);
    }
    tableMeasures[trendMeasureName].Expression = trendMeasureDax;

}

// Format all the measures of the table using DaxFormatter.com
tableMeasures.FormatDax(false);

