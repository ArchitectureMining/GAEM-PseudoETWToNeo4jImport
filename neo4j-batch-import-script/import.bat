C:/Neo4j/neo4j-community-3.2.1/bin/neo4j-admin import ^
--database="import-test" ^
--mode=csv ^
--id-type=INTEGER ^
--ignore-missing-nodes=true ^
--nodes:class="classNodesHeader.csv,classNodes.csv" ^
--nodes:dir="dirNodesHeader.csv,dirNodes.csv" ^
--nodes:file="fileNodesHeader.csv,fileNodes.csv" ^
--nodes:function="functionNodesHeader.csv,functionNodes.csv" ^
--nodes:namespace="namespaceNodesHeader.csv,namespaceNodes.csv" ^
--nodes:struct="structNodesHeader.csv,structNodes.csv" ^
--nodes:startEvent:event="startEventNodesHeader.csv,startEventNodes.csv" ^
--nodes:stopEvent:event="stopEventNodesHeader.csv,stopEventNodes.csv" ^
--nodes:call="callNodesHeader.csv,callNodes.csv" ^
--relationships:IN="relationsHeader.csv,inRelations.csv" ^
--relationships:INSTANCEOF="relationsHeader.csv,instanceofRelations.csv" ^
--relationships:INVOKES="relationsHeader.csv,invokesRelations.csv" ^
--relationships:STARTS="relationsHeader.csv,startsRelations.csv" ^
--relationships:STOPS="relationsHeader.csv,stopsRelations.csv"