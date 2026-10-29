package main

import (
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"slices"
	"time"

	"sigs.k8s.io/kind/pkg/build/nodeimage"
	"sigs.k8s.io/kind/pkg/cluster"
	"sigs.k8s.io/kind/pkg/cluster/nodeutils"
	"sigs.k8s.io/kind/pkg/cmd/kind/version"
)

type request struct {
	Operation        string   `json:"operation"`
	Name             string   `json:"name"`
	Provider         string   `json:"provider"`
	Image            string   `json:"image"`
	ConfigPath       string   `json:"configPath"`
	KubeConfigPath   string   `json:"kubeConfigPath"`
	WaitMilliseconds int64    `json:"waitMilliseconds"`
	Retain           bool     `json:"retain"`
	InternalAddress  bool     `json:"internalAddress"`
	Path             string   `json:"path"`
	Images           []string `json:"images"`
	Nodes            []string `json:"nodes"`
	KubernetesSource string   `json:"kubernetesSource"`
	BaseImage        string   `json:"baseImage"`
	Type             string   `json:"type"`
	Architecture     string   `json:"architecture"`
}

type response struct {
	Data  any    `json:"data,omitempty"`
	Error string `json:"error,omitempty"`
}

var signalInitializationError = prepareSignalHandlers()

func main() {}

func handleJSON(input []byte) (output []byte) {
	defer func() {
		if value := recover(); value != nil {
			output, _ = json.Marshal(response{Error: fmt.Sprintf("Kind native panic: %v", value)})
		}
	}()
	var r request
	if err := json.Unmarshal(input, &r); err != nil {
		output, _ = json.Marshal(response{Error: err.Error()})
		return
	}
	data, err := execute(r)
	result := response{Data: data}
	if err != nil {
		result = response{Error: err.Error()}
	}
	output, err = json.Marshal(result)
	if err != nil {
		output, _ = json.Marshal(response{Error: err.Error()})
	}
	return
}

func newProvider(name string) (*cluster.Provider, error) {
	if name == "" {
		name = os.Getenv("KIND_EXPERIMENTAL_PROVIDER")
	}
	switch name {
	case "":
		return cluster.NewProvider(), nil
	case "docker":
		return cluster.NewProvider(cluster.ProviderWithDocker()), nil
	case "podman":
		return cluster.NewProvider(cluster.ProviderWithPodman()), nil
	case "nerdctl":
		return cluster.NewProvider(cluster.ProviderWithNerdctl("")), nil
	default:
		return nil, fmt.Errorf("unsupported container provider %q", name)
	}
}

func execute(r request) (any, error) {
	if signalInitializationError != nil {
		return nil, signalInitializationError
	}
	if r.Name == "" {
		r.Name = cluster.DefaultName
	}
	switch r.Operation {
	case "version":
		return version.Version(), nil
	case "buildNodeImage":
		options := []nodeimage.Option{nodeimage.WithKubeParam(r.KubernetesSource), nodeimage.WithBuildType(r.Type), nodeimage.WithArch(r.Architecture)}
		if r.Image != "" {
			options = append(options, nodeimage.WithImage(r.Image))
		}
		if r.BaseImage != "" {
			options = append(options, nodeimage.WithBaseImage(r.BaseImage))
		}
		return struct{}{}, nodeimage.Build(options...)
	case "create", "delete", "clusters", "nodes", "allNodes", "kubeConfig", "exportKubeConfig", "exportLogs", "loadDockerImages", "loadImageArchive":
	default:
		return nil, fmt.Errorf("unknown Kind operation %q", r.Operation)
	}
	p, err := newProvider(r.Provider)
	if err != nil {
		return nil, err
	}
	switch r.Operation {
	case "create":
		if r.WaitMilliseconds < 0 || r.WaitMilliseconds > int64(^uint64(0)>>1)/int64(time.Millisecond) {
			return nil, fmt.Errorf("wait must be a nonnegative Go duration")
		}
		options := []cluster.CreateOption{cluster.CreateWithRetain(r.Retain), cluster.CreateWithKubeconfigPath(r.KubeConfigPath), cluster.CreateWithWaitForReady(time.Duration(r.WaitMilliseconds) * time.Millisecond)}
		if r.Image != "" {
			options = append(options, cluster.CreateWithNodeImage(r.Image))
		}
		if r.ConfigPath != "" {
			options = append(options, cluster.CreateWithConfigFile(r.ConfigPath))
		}
		return struct{}{}, p.Create(r.Name, options...)
	case "delete":
		return struct{}{}, p.Delete(r.Name, r.KubeConfigPath)
	case "clusters":
		names, err := p.List()
		if names == nil {
			names = []string{}
		}
		return names, err
	case "nodes", "allNodes":
		clusters := []string{r.Name}
		if r.Operation == "allNodes" {
			clusters, err = p.List()
			if err != nil {
				return nil, err
			}
		}
		names := []string{}
		for _, name := range clusters {
			nodes, err := p.ListNodes(name)
			if err != nil {
				return nil, err
			}
			for _, node := range nodes {
				names = append(names, node.String())
			}
		}
		return names, nil
	case "kubeConfig":
		return p.KubeConfig(r.Name, r.InternalAddress)
	case "exportKubeConfig":
		return struct{}{}, p.ExportKubeConfig(r.Name, r.Path, r.InternalAddress)
	case "exportLogs":
		path := r.Path
		if path == "" {
			path, err = os.MkdirTemp("", "kind-net-logs-")
			if err != nil {
				return nil, err
			}
		}
		return path, p.CollectLogs(r.Name, path)
	case "loadImageArchive":
		return struct{}{}, loadArchive(p, r, r.Path)
	case "loadDockerImages":
		if len(r.Images) == 0 {
			return nil, fmt.Errorf("at least one Docker image is required")
		}
		dir, err := os.MkdirTemp("", "kind-net-images-")
		if err != nil {
			return nil, err
		}
		defer os.RemoveAll(dir)
		archive := filepath.Join(dir, "images.tar")
		args := append([]string{"save", "-o", archive}, r.Images...)
		if output, err := exec.Command("docker", args...).CombinedOutput(); err != nil {
			return nil, fmt.Errorf("docker save: %w: %s", err, output)
		}
		return struct{}{}, loadArchive(p, r, archive)
	}
	return nil, fmt.Errorf("unknown operation")
}

func loadArchive(p *cluster.Provider, r request, path string) error {
	// Validate the archive and every selected node before changing any node.
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	file.Close()
	nodes, err := p.ListInternalNodes(r.Name)
	if err != nil {
		return err
	}
	if len(nodes) == 0 {
		return fmt.Errorf("no nodes found for cluster %q", r.Name)
	}
	for _, name := range r.Nodes {
		found := false
		for _, node := range nodes {
			if node.String() == name {
				found = true
				break
			}
		}
		if !found {
			return fmt.Errorf("unknown node: %s", name)
		}
	}
	for _, node := range nodes {
		if len(r.Nodes) > 0 && !slices.Contains(r.Nodes, node.String()) {
			continue
		}
		file, err := os.Open(path)
		if err != nil {
			return err
		}
		err = nodeutils.LoadImageArchive(node, file)
		file.Close()
		if err != nil {
			return err
		}
	}
	return nil
}
