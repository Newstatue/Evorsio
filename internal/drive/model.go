package drive

import (
	"github.com/newstatue/evorsio/internal/resource"
)

type Folder struct {
	resource.Resource
}

type Symlink struct {
	resource.Resource

	TargetID string
}

type Entry struct {
	ParentID string
	ChildID  string
}

func NewFolder(name string) Folder {
	r := resource.New(name, resource.TypeFolder)
	return Folder{
		Resource: r,
	}
}

func NewSymlink(name string, targetID string) Symlink {
	r := resource.New(name, resource.TypeSymlink)
	return Symlink{
		Resource: r,
		TargetID: targetID,
	}
}
