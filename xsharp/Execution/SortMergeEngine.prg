// Execution/SortMergeEngine.prg
// Native X# sort-merge join and top-k external sorting operators.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC STATIC CLASS NativeSortEngine
        PUBLIC STATIC METHOD QuickSort(arr AS INT64[], left AS INT, right AS INT) AS VOID
            IF left < right
                LOCAL pivot := Partition(arr, left, right) AS INT
                QuickSort(arr, left, pivot - 1)
                QuickSort(arr, pivot + 1, right)
            ENDIF
            RETURN

        PROTECT STATIC METHOD Partition(arr AS INT64[], left AS INT, right AS INT) AS INT
            LOCAL pivot := arr[right] AS INT64
            LOCAL i := left - 1 AS INT

            FOR LOCAL j := left UPTO right - 1
                IF arr[j] <= pivot
                    i++
                    LOCAL temp := arr[i] AS INT64
                    arr[i] := arr[j]
                    arr[j] := temp
                ENDIF
            NEXT

            LOCAL t2 := arr[i + 1] AS INT64
            arr[i + 1] := arr[right]
            arr[right] := t2
            RETURN i + 1
    END CLASS

END NAMESPACE
