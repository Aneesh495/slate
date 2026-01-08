#include "slate.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

struct slate_db {
    char* wal_path;
    uint64_t next_tx_id;
};

struct slate_tx {
    uint64_t tx_id;
    uint64_t read_timestamp;
    bool is_active;
};

typedef struct {
    char* string_val;
    int64_t int_val;
    double float_val;
    bool is_null;
} slate_cell_t;

struct slate_result {
    int column_count;
    char** column_names;
    int row_count;
    slate_cell_t** rows;
    double execution_time_ms;
    char* error_message;
};

const char* slate_version(void) {
    return "1.0.0-native-c";
}

slate_db_t* slate_open(const char* wal_path) {
    slate_db_t* db = (slate_db_t*)malloc(sizeof(slate_db_t));
    if (!db) return NULL;
    db->wal_path = wal_path ? strdup(wal_path) : NULL;
    db->next_tx_id = 1000;
    return db;
}

void slate_close(slate_db_t* db) {
    if (!db) return;
    if (db->wal_path) free(db->wal_path);
    free(db);
}

slate_tx_t* slate_begin_tx(slate_db_t* db) {
    if (!db) return NULL;
    slate_tx_t* tx = (slate_tx_t*)malloc(sizeof(slate_tx_t));
    if (!tx) return NULL;
    tx->tx_id = ++db->next_tx_id;
    tx->read_timestamp = db->next_tx_id;
    tx->is_active = true;
    return tx;
}

slate_status_t slate_commit_tx(slate_db_t* db, slate_tx_t* tx) {
    (void)db;
    if (!tx || !tx->is_active) return SLATE_ERROR;
    tx->is_active = false;
    free(tx);
    return SLATE_OK;
}

slate_status_t slate_abort_tx(slate_db_t* db, slate_tx_t* tx) {
    (void)db;
    if (!tx) return SLATE_ERROR;
    tx->is_active = false;
    free(tx);
    return SLATE_OK;
}

slate_result_t* slate_execute(slate_db_t* db, const char* sql) {
    return slate_execute_tx(db, sql, NULL);
}

slate_result_t* slate_execute_tx(slate_db_t* db, const char* sql, slate_tx_t* tx) {
    (void)db;
    (void)tx;

    slate_result_t* res = (slate_result_t*)calloc(1, sizeof(slate_result_t));
    if (!res) return NULL;

    res->execution_time_ms = 0.42;

    if (!sql || strlen(sql) == 0) {
        res->column_count = 0;
        res->row_count = 0;
        return res;
    }

    // Mock/stub native execution parser for C FFI demonstration
    if (strstr(sql, "SELECT") != NULL || strstr(sql, "select") != NULL) {
        res->column_count = 2;
        res->column_names = (char**)malloc(sizeof(char*) * 2);
        res->column_names[0] = strdup("id");
        res->column_names[1] = strdup("name");

        res->row_count = 1;
        res->rows = (slate_cell_t**)malloc(sizeof(slate_cell_t*));
        res->rows[0] = (slate_cell_t*)malloc(sizeof(slate_cell_t) * 2);

        res->rows[0][0].int_val = 1;
        res->rows[0][0].float_val = 1.0;
        res->rows[0][0].string_val = strdup("1");
        res->rows[0][0].is_null = false;

        res->rows[0][1].int_val = 0;
        res->rows[0][1].float_val = 0.0;
        res->rows[0][1].string_val = strdup("C_FFI_Record");
        res->rows[0][1].is_null = false;
    } else {
        res->column_count = 0;
        res->row_count = 0;
    }

    return res;
}

int slate_result_column_count(const slate_result_t* res) {
    return res ? res->column_count : 0;
}

const char* slate_result_column_name(const slate_result_t* res, int col_idx) {
    if (!res || col_idx < 0 || col_idx >= res->column_count) return NULL;
    return res->column_names[col_idx];
}

int slate_result_row_count(const slate_result_t* res) {
    return res ? res->row_count : 0;
}

bool slate_result_is_null(const slate_result_t* res, int row_idx, int col_idx) {
    if (!res || row_idx < 0 || row_idx >= res->row_count || col_idx < 0 || col_idx >= res->column_count) return true;
    return res->rows[row_idx][col_idx].is_null;
}

int64_t slate_result_get_int64(const slate_result_t* res, int row_idx, int col_idx) {
    if (slate_result_is_null(res, row_idx, col_idx)) return 0;
    return res->rows[row_idx][col_idx].int_val;
}

double slate_result_get_float64(const slate_result_t* res, int row_idx, int col_idx) {
    if (slate_result_is_null(res, row_idx, col_idx)) return 0.0;
    return res->rows[row_idx][col_idx].float_val;
}

const char* slate_result_get_string(const slate_result_t* res, int row_idx, int col_idx) {
    if (slate_result_is_null(res, row_idx, col_idx)) return NULL;
    return res->rows[row_idx][col_idx].string_val;
}

double slate_result_execution_time_ms(const slate_result_t* res) {
    return res ? res->execution_time_ms : 0.0;
}

void slate_result_free(slate_result_t* res) {
    if (!res) return;
    for (int c = 0; c < res->column_count; c++) {
        if (res->column_names[c]) free(res->column_names[c]);
    }
    if (res->column_names) free(res->column_names);

    for (int r = 0; r < res->row_count; r++) {
        for (int c = 0; c < res->column_count; c++) {
            if (res->rows[r][c].string_val) free(res->rows[r][c].string_val);
        }
        free(res->rows[r]);
    }
    if (res->rows) free(res->rows);
    if (res->error_message) free(res->error_message);
    free(res);
}
