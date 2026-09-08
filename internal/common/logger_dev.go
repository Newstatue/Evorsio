//go:build dev

package common

import (
	"log/slog"
	"os"
	"time"

	"github.com/lmittmann/tint"
	"github.com/mattn/go-colorable"
)

func InitLogger(level slog.Level) {
	l := slog.New(tint.NewTextHandler(colorable.NewColorable(os.Stderr), &tint.Options{
		Level:      level,
		TimeFormat: time.RFC3339,
	}))
	slog.SetDefault(l)
}
