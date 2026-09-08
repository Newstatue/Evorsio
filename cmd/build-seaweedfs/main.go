package main

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/newstatue/evorsio/internal/constant"
)

func main() {
	out, err := exec.Command("mise", "where", "github:seaweedfs/seaweedfs").Output()
	if err != nil {
		panic(err)
	}

	dir := strings.TrimSpace(string(out))

	srcName := "weed"
	dstName := "weed"

	if runtime.GOOS == "windows" {
		srcName = "weed.exe"
		dstName = "weed.exe"
	}

	src := filepath.Join(dir, srcName)

	dstDir := filepath.Join(constant.BuildDirTMP)

	dst := filepath.Join(dstDir, dstName)

	if err := os.MkdirAll(dstDir, 0755); err != nil {
		panic(err)
	}

	data, err := os.ReadFile(src)
	if err != nil {
		panic(err)
	}

	if err := os.WriteFile(dst, data, 0755); err != nil {
		panic(err)
	}

	fmt.Printf("SeaweedFS: %s -> %s\n", src, dst)
}
