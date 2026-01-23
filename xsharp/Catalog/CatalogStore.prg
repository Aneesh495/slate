// Catalog/CatalogStore.prg
// Native X# system catalog manager tracking schemas, columns, and secondary indexes.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Catalog

    PUBLIC CLASS NativeTableMetadata
        PUBLIC TableName AS STRING
        PUBLIC Version AS INT
        PUBLIC ColumnNames AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Version := 1
            SELF:ColumnNames := List<STRING>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeCatalogStore
        PROTECT _tables AS Dictionary<STRING, NativeTableMetadata>

        PUBLIC CONSTRUCTOR()
            _tables := Dictionary<STRING, NativeTableMetadata>{}
            RETURN

        PUBLIC METHOD CreateTable(name AS STRING) AS VOID
            _tables:Add(name, NativeTableMetadata{name})
            RETURN
    END CLASS

END NAMESPACE
