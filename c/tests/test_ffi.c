#include "slate.h"
#include <stdio.h>
#include <assert.h>
#include <string.h>

int main(void) {
    printf("Testing Slate C FFI Library...\n");

    const char* ver = slate_version();
    assert(ver != NULL);
    assert(strstr(ver, "1.0.0") != NULL);
    printf("  [PASS] Version check: %s\n", ver);

    slate_db_t* db = slate_open("wal.log");
    assert(db != NULL);
    printf("  [PASS] Database open\n");

    slate_tx_t* tx = slate_begin_tx(db);
    assert(tx != NULL);
    slate_status_t commit_st = slate_commit_tx(db, tx);
    assert(commit_st == SLATE_OK);
    printf("  [PASS] Transaction lifecycle\n");

    slate_result_t* res = slate_execute(db, "SELECT id, name FROM users;");
    assert(res != NULL);
    assert(slate_result_column_count(res) == 2);
    assert(strcmp(slate_result_column_name(res, 0), "id") == 0);
    assert(strcmp(slate_result_column_name(res, 1), "name") == 0);
    assert(slate_result_row_count(res) == 1);
    assert(slate_result_get_int64(res, 0, 0) == 1);
    assert(strcmp(slate_result_get_string(res, 0, 1), "C_FFI_Record") == 0);
    printf("  [PASS] Query execution and column extraction\n");

    slate_result_free(res);
    slate_close(db);
    printf("  [PASS] Database closed cleanly\n");

    printf("All C FFI unit tests passed successfully!\n");
    return 0;
}
