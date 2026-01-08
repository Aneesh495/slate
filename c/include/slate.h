#ifndef SLATE_H
#define SLATE_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct slate_db slate_db_t;
typedef struct slate_tx slate_tx_t;
typedef struct slate_result slate_result_t;

typedef enum {
    SLATE_OK = 0,
    SLATE_ERROR = 1,
    SLATE_PARSE_ERROR = 2,
    SLATE_CONFLICT = 3
} slate_status_t;

// Lifecycle
slate_db_t* slate_open(const char* wal_path);
void slate_close(slate_db_t* db);
const char* slate_version(void);

// Transactions
slate_tx_t* slate_begin_tx(slate_db_t* db);
slate_status_t slate_commit_tx(slate_db_t* db, slate_tx_t* tx);
slate_status_t slate_abort_tx(slate_db_t* db, slate_tx_t* tx);

// Execution
slate_result_t* slate_execute(slate_db_t* db, const char* sql);
slate_result_t* slate_execute_tx(slate_db_t* db, const char* sql, slate_tx_t* tx);

// Result extraction
int slate_result_column_count(const slate_result_t* res);
const char* slate_result_column_name(const slate_result_t* res, int col_idx);
int slate_result_row_count(const slate_result_t* res);
bool slate_result_is_null(const slate_result_t* res, int row_idx, int col_idx);
int64_t slate_result_get_int64(const slate_result_t* res, int row_idx, int col_idx);
double slate_result_get_float64(const slate_result_t* res, int row_idx, int col_idx);
const char* slate_result_get_string(const slate_result_t* res, int row_idx, int col_idx);
double slate_result_execution_time_ms(const slate_result_t* res);
void slate_result_free(slate_result_t* res);

#ifdef __cplusplus
}
#endif

#endif // SLATE_H
