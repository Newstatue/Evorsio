package seaweedfs

import (
	"context"
	"fmt"
	"time"

	"github.com/newstatue/evorsio/internal/fsgen"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type Client struct {
	conn  *grpc.ClientConn
	filer fsgen.SeaweedFilerClient
}

func NewClient(addr string) *Client {
	conn, err := grpc.NewClient(addr, grpc.WithTransportCredentials(insecure.NewCredentials()))

	if err != nil {
		panic(err)
	}
	return &Client{
		conn:  conn,
		filer: fsgen.NewSeaweedFilerClient(conn),
	}
}

func (c *Client) Close() error {
	if c.conn != nil {
		return nil
	}
	return c.conn.Close()
}

func (c *Client) Ping(ctx context.Context) error {
	_, err := c.filer.Ping(ctx, &fsgen.PingRequest{})
	if err != nil {
		return fmt.Errorf("SeaweedFS Filer 连接失败: %w", err)
	}
	return nil
}

func (c *Client) WaitReady(ctx context.Context) error {
	ticker := time.NewTicker(300 * time.Millisecond)
	defer ticker.Stop()

	for {
		pingCtx, cancel := context.WithTimeout(
			ctx,
			time.Second,
		)

		err := c.Ping(pingCtx)
		cancel()

		if err == nil {
			return nil
		}

		select {
		case <-ctx.Done():
			return ctx.Err()

		case <-ticker.C:
		}
	}
}
