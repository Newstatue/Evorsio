package main

import (
	"database/sql"
	"log/slog"

	"github.com/newstatue/evorsio"
	"github.com/newstatue/evorsio/internal/common"
	"github.com/newstatue/evorsio/internal/constant"
	"github.com/newstatue/evorsio/internal/seaweedfs"
	"github.com/wailsapp/wails/v3/pkg/application"
	_ "modernc.org/sqlite"
)

func init() {
	common.InitLogger(slog.LevelDebug)
}

const (
	kErr            = string(constant.LogArgError)
	kComponent      = string(constant.LogArgComponent)
	vComponentApp   = string(constant.ComponentApp)
	vComponentFS    = string(constant.ComponentFS)
	vComponentWails = string(constant.ComponentWails)
)

func main() {
	l := slog.Default()
	al := l.With(kComponent, vComponentApp)

	cfg, err := common.NewConfig()
	if err != nil {
		l.Error("配置出错", kErr, err)
		return
	}

	db, err := sql.Open(cfg.DB.Driver, cfg.DB.DSN)
	if err != nil {
		l.Error("数据库初始化失败", kErr, err)
		return
	}
	defer func(db *sql.DB) {
		_ = db.Close()
	}(db)

	fs := seaweedfs.New(l.With(kComponent, vComponentFS))

	app := application.New(application.Options{
		Name:        "app",
		Description: "A demo of using raw HTML & CSS",
		Logger:      l.With(kComponent, vComponentWails),
		Services: []application.Service{
			application.NewService(fs),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(evorsio.Assets),
		},
		Mac: application.MacOptions{
			ApplicationShouldTerminateAfterLastWindowClosed: true,
		},
	})

	app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title:  "Window 1",
		Width:  1000,
		Height: 618,
		Mac: application.MacWindow{
			InvisibleTitleBarHeight: 50,
			Backdrop:                application.MacBackdropTranslucent,
			TitleBar:                application.MacTitleBarHiddenInset,
		},
		BackgroundColour: application.NewRGB(6, 7, 15),
		URL:              "/",
	})

	ctx := app.Context()

	if err := db.PingContext(ctx); err != nil {
		al.ErrorContext(ctx, "数据库连接失败", kErr, err)
		return
	}

	if err := fs.Start(ctx, cfg.FS.Path, cfg.FS.DataDir); err != nil {
		al.ErrorContext(ctx, "对象存储连接失败", kErr, err)
		return
	}

	if err := app.Run(); err != nil {
		al.Error("APP 退出异常", kErr, err)
		return
	}

	al.Info("APP 正常退出")
}
