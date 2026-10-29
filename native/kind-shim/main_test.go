package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"testing"
	"time"
)

func TestProtocol(t *testing.T) {
	for _, input := range []string{`not json`, `null`, `{}`, `{"operation":"unsupported"}`, `{"operation":"clusters","provider":"invalid"}`} {
		var r response
		if err := json.Unmarshal(handleJSON([]byte(input)), &r); err != nil {
			t.Fatal(err)
		}
		if r.Error == "" {
			t.Fatalf("expected action error for %s", input)
		}
	}
	var r struct {
		Data  string `json:"data"`
		Error string `json:"error"`
	}
	if err := json.Unmarshal(handleJSON([]byte(`{"operation":"version"}`)), &r); err != nil {
		t.Fatal(err)
	}
	if r.Error != "" || r.Data == "" {
		t.Fatalf("invalid version response: %+v", r)
	}
}

func TestProviderSelection(t *testing.T) {
	for _, name := range []string{"docker", "podman", "nerdctl"} {
		if _, err := newProvider(name); err != nil {
			t.Fatal(err)
		}
	}
	t.Setenv("KIND_EXPERIMENTAL_PROVIDER", "invalid")
	if _, err := newProvider(""); err == nil {
		t.Fatal("ignored provider environment")
	}
	if _, err := newProvider("docker"); err != nil {
		t.Fatal("explicit provider must override environment")
	}
}

func TestLifecycle(t *testing.T) {
	if os.Getenv("KIND_SHIM_TEST_CLUSTER") != "true" {
		t.Skip("set KIND_SHIM_TEST_CLUSTER=true")
	}
	dir := t.TempDir()
	r := request{Name: fmt.Sprintf("kind-net-go-%d", time.Now().UnixNano()), Provider: "docker", KubeConfigPath: filepath.Join(dir, "kubeconfig"), WaitMilliseconds: 180000}
	t.Cleanup(func() {
		r.Operation = "delete"
		if _, err := execute(r); err != nil {
			t.Error(err)
		}
	})
	r.Operation = "create"
	if _, err := execute(r); err != nil {
		t.Fatal(err)
	}
	r.Operation = "nodes"
	data, err := execute(r)
	if err != nil {
		t.Fatal(err)
	}
	if len(data.([]string)) != 1 {
		t.Fatalf("expected one node, got %v", data)
	}
	r.Operation = "kubeConfig"
	if data, err := execute(r); err != nil || data == "" {
		t.Fatalf("kubeconfig: %v %v", data, err)
	}
}

func TestInvalidOperationInputs(t *testing.T) {
	for _, r := range []request{
		{Operation: "create", Provider: "docker", WaitMilliseconds: -1},
		{Operation: "create", Provider: "docker", WaitMilliseconds: 9223372036854775807},
		{Operation: "create", Provider: "docker", ConfigPath: filepath.Join(t.TempDir(), "missing")},
		{Operation: "loadDockerImages", Provider: "docker"},
		{Operation: "loadImageArchive", Provider: "docker", Path: filepath.Join(t.TempDir(), "missing")},
		{Operation: "buildNodeImage", Type: "release", KubernetesSource: "invalid-version"},
	} {
		if _, err := execute(r); err == nil {
			t.Errorf("expected error for %+v", r)
		}
	}
}
