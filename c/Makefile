CC ?= clang
CFLAGS ?= -Wall -Wextra -O2 -Iinclude -fPIC
LDFLAGS ?= -shared

BUILD_DIR = build
TARGET_LIB = $(BUILD_DIR)/libslate.dylib
TEST_BIN = $(BUILD_DIR)/test_ffi

all: $(TARGET_LIB) $(TEST_BIN)

$(BUILD_DIR):
	mkdir -p $(BUILD_DIR)

$(TARGET_LIB): src/slate_ffi.c include/slate.h | $(BUILD_DIR)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ src/slate_ffi.c

$(TEST_BIN): tests/test_ffi.c src/slate_ffi.c include/slate.h | $(BUILD_DIR)
	$(CC) $(CFLAGS) -o $@ tests/test_ffi.c src/slate_ffi.c

test: $(TEST_BIN)
	./$(TEST_BIN)

clean:
	rm -rf $(BUILD_DIR)

.PHONY: all test clean
