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

type Server struct {
	mu sync.Mutex

	cmd *exec.Cmd
	l   *slog.Logger
}

func NewServer(l *slog.Logger) *Server {
	return &Server{l: l}
}

func (s *Server) Start(ctx context.Context, path string, dataDir string) error {
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

	if err := cmd.Start(); err != nil {
		return err
	}

	s.cmd = cmd

	go pipeLog(s.l, stdout)
	go pipeLog(s.l, stderr)
	go func() {
		err := cmd.Wait()
		s.mu.Lock()
		defer s.mu.Unlock()
		if s.cmd != cmd {
			return
		}
		s.cmd = nil
		if err != nil {
			s.l.ErrorContext(ctx, "SeaweedFS Server 已退出", KErr, err)
		}
	}()
	return nil
}

func (s *Server) Stop() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.cmd == nil || s.cmd.Process == nil {
		return nil
	}

	return s.cmd.Process.Signal(os.Interrupt)
}

func (s *Server) Running() bool {
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
		data[KComponent] = VComponentFS
		out, _ := json.Marshal(data)
		_, _ = os.Stdout.Write(out)
		_, _ = os.Stdout.WriteString("\n")
	}

	if err := scanner.Err(); err != nil {
		logger.Error("读取日志失败", KErr, err)
	}
}
