// ServerDispatcher.prg
// HTTP endpoint dispatching and method validation in native X#

USING System

BEGIN NAMESPACE Slate.Server

    /// <summary>
    /// HTTP route classification and status code mapping implemented in native X#.
    /// </summary>
    PUBLIC CLASS HttpRouteDispatcher
        PUBLIC STATIC METHOD ClassifyRoute(method AS STRING, path AS STRING) AS INT
            IF method == "POST" .AND. path == "/v1/query"
                RETURN 1 // Query execution
            ELSEIF method == "GET" .AND. path == "/v1/status"
                RETURN 2 // Status / health
            ELSEIF method == "GET" .AND. path == "/v1/catalog"
                RETURN 3 // Schema catalog
            ELSEIF method == "POST" .AND. path:StartsWith("/v1/tx")
                RETURN 4 // Transaction management
            ENDIF

            RETURN 0 // Not found / unsupported
    END CLASS

END NAMESPACE
