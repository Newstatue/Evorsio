package constant

const BuildDirTMP = "tmp"

type Component string

const (
	ComponentApp   Component = "app"
	ComponentFS    Component = "fs"
	ComponentWails Component = "wails"
)

type LogArg string

const (
	LogArgError     LogArg = "error"
	LogArgComponent LogArg = "component"
)
