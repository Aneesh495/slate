# Makefile for Slate HTAP Database Engine

DOTNET ?= dotnet
PYTHON ?= python3
CONFIGURATION ?= Release

.PHONY: all build test bench demo server verify verify-no-em-dash clean

all: build

build:
	$(DOTNET) build -c $(CONFIGURATION)

test:
	$(DOTNET) test -c $(CONFIGURATION) --verbosity normal

bench:
	$(DOTNET) run -c $(CONFIGURATION) --project bench/Slate.Benchmarks

demo:
	$(DOTNET) run -c $(CONFIGURATION) --project src/Slate.Cli -- demo

server:
	$(DOTNET) run -c $(CONFIGURATION) --project src/Slate.Server

verify-no-em-dash:
	@$(PYTHON) tools/audit_em_dashes.py

verify: build test verify-no-em-dash
	@echo "All verification gates passed with zero defects."

clean:
	$(DOTNET) clean
	rm -rf bin obj src/*/bin src/*/obj tests/*/bin tests/*/obj bench/*/bin bench/*/obj
