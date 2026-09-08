//go:build !dev

package common

import (
	"log/slog"
	"os"

	"github.com/mattn/go-colorable"
)

func InitLogger(level slog.Level) {
	l := slog.New(slog.NewJSONHandler(colorable.NewColorable(os.Stderr), &slog.HandlerOptions{
		Level: level,
	}))
	slog.SetDefault(l)
}
