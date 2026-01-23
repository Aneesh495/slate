// Transactions/EpochReclamation.prg
// Native X# epoch-based memory reclamation for garbage collecting
// retired row versions under snapshot isolation.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Transactions

    PUBLIC CLASS NativeEpochManager
        PROTECT _globalEpoch AS INT64
        PROTECT _lock AS OBJECT

        PUBLIC CONSTRUCTOR()
            _globalEpoch := 1
            _lock := OBJECT{}
            RETURN

        PUBLIC METHOD AdvanceEpoch() AS INT64
            BEGIN LOCK _lock
                _globalEpoch++
                RETURN _globalEpoch
            END LOCK
    END CLASS

END NAMESPACE
