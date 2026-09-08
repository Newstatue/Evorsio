//go:build !dev

package seaweedfs

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"os"
	"os/exec"
	"sync"
)

type Service struct {
	mu  sync.Mutex
	cmd *exec.Cmd
	l   *slog.Logger
}

func New(l *slog.Logger) *Service {
	return &Service{l: l}
}

func (s *Service) Start(ctx context.Context, path string, dataDir string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.cmd != nil && s.cmd.Process != nil {
		return fmt.Errorf("SeaweedFS 已经启动")
	}

	cmd := exec.CommandContext(ctx, path, "-log_json", "server", "-dir="+dataDir)

	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}

	stderr, err := cmd.StderrPipe()
	if err != nil {
		return err
	}

	go pipeLog(s.l, stdout)
	go pipeLog(s.l, stderr)

	if err := cmd.Start(); err != nil {
		return err
	}

	s.cmd = cmd
	go func() {
		_ = cmd.Wait()
		s.mu.Lock()
		defer s.mu.Unlock()

		if s.cmd == cmd {
			s.cmd = nil
		}
	}()
	return nil
}

func (s *Service) Stop() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.cmd == nil || s.cmd.Process == nil {
		return nil
	}

	return s.cmd.Process.Signal(os.Interrupt)
}

func (s *Service) Running() bool {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.cmd != nil && s.cmd.Process != nil
}

func pipeLog(logger *slog.Logger, r io.Reader) {
	scanner := bufio.NewScanner(r)

	for scanner.Scan() {
		line := scanner.Text()
		var data map[string]any
		if err := json.Unmarshal([]byte(line), &data); err != nil {
			logger.Debug(line)
			continue
		}
		data["component"] = "fs"
		out, _ := json.Marshal(data)
		_, _ = os.Stdout.Write(out)
		_, _ = os.Stdout.WriteString("\n")
	}

	if err := scanner.Err(); err != nil {
		logger.Error("读取日志失败", "error", err)
	}
}
