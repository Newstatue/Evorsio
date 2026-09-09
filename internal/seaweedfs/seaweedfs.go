package seaweedfs

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"github.com/newstatue/evorsio/internal/common"
	"github.com/newstatue/evorsio/internal/constant"
)

const (
	KErr = string(constant.LogArgError)
)

type SeaweedFS struct {
	mu sync.RWMutex

	l   *slog.Logger
	cfg *common.FSConfig

	server *Server
	client *Client
}

func New(cfg *common.FSConfig, l *slog.Logger) *SeaweedFS {
	return &SeaweedFS{
		cfg:    cfg,
		l:      l,
		server: NewServer(l),
	}
}

func (s *SeaweedFS) Start(ctx context.Context) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.client != nil {
		return fmt.Errorf("SeaweedFS 已经启动")
	}

	if err := s.server.Start(ctx, s.cfg.Path, s.cfg.DataDir); err != nil {
		return fmt.Errorf("启动 SeaweedFS 服务失败: %w", err)
	}

	client := NewClient(s.cfg.Addr)
	readyCtx, cancel := context.WithTimeout(ctx, 15*time.Second)
	defer cancel()

	if err := client.WaitReady(readyCtx); err != nil {
		_ = client.Close()
		_ = s.server.Stop()

		return fmt.Errorf("等待 SeaweedFS 就绪失败: %w", err)
	}

	s.client = client

	s.l.Info("SeaweedFS Filer 已就绪")

	return nil
}

func (s *SeaweedFS) Close() error {
	s.mu.Lock()
	defer s.mu.Unlock()

	var errs []error

	if s.client != nil {
		if err := s.client.Close(); err != nil {
			errs = append(errs, err)
		}

		s.client = nil
	}

	if err := s.server.Stop(); err != nil {
		errs = append(errs, err)
	}

	return errors.Join(errs...)
}

func (s *SeaweedFS) Client() (*Client, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	if s.client == nil {
		return nil, fmt.Errorf("SeaweedFS 尚未启动")
	}

	return s.client, nil
}

func (s *SeaweedFS) Ready() bool {
	s.mu.RLock()
	defer s.mu.RUnlock()

	return s.client != nil && s.server.Running()
}
