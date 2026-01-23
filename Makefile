# Makefile for Slate HTAP Database Engine

DOTNET ?= dotnet
PYTHON ?= python3
CONFIGURATION ?= Release

.PHONY: all build test test-c test-python test-all bench demo repl server verify verify-no-em-dash clean

all: build

build:
	$(DOTNET) build -c $(CONFIGURATION)

test:
	$(DOTNET) test -c $(CONFIGURATION) --verbosity normal

test-c:
	@$(MAKE) -C c test

test-python:
	@PYTHONPATH=python $(PYTHON) -m unittest discover -s python/tests

test-all: test test-c test-python

bench:
	$(DOTNET) run -c $(CONFIGURATION) --project bench/Slate.Benchmarks

demo:
	$(DOTNET) run -c $(CONFIGURATION) --project src/Slate.Cli -- demo

repl:
	$(DOTNET) run -c $(CONFIGURATION) --project src/Slate.Cli

server:
	$(DOTNET) run -c $(CONFIGURATION) --project src/Slate.Server

verify-no-em-dash:
	@$(PYTHON) tools/audit_em_dashes.py

verify: build test test-c test-python verify-no-em-dash
	@echo "All verification gates passed with zero defects."

clean:
	$(DOTNET) clean
	@$(MAKE) -C c clean || true
	rm -rf bin obj src/*/bin src/*/obj tests/*/bin tests/*/obj bench/*/bin bench/*/obj
