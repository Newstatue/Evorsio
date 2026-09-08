package common

import (
	"errors"
	"fmt"
	"os"
	"reflect"
	"strconv"
	"strings"
)

type Config struct {
	App  AppConfig
	HTTP HTTPConfig
	DB   DBConfig
	FS   FSConfig

	dict    map[string]any
	errDict map[string]error
}

type AppConfig struct {
	Environment string `env:"ENVIRONMENT" envDefault:"dev"`
	Name        string `env:"NAME" envDefault:"evorsio"`
	Version     string `env:"VERSION"`
}

type HTTPConfig struct {
	Addr string `env:"ADDR" envDefault:":8080"`
}

type DBConfig struct {
	Driver string `env:"DRIVER" envDefault:"sqlite"`
	DSN    string `env:"DSN"`
}

type FSConfig struct {
	Path    string `env:"FS_PATH"`
	DataDir string `env:"FS_DATA_DIR"`
}

func NewConfig() (*Config, error) {
	cfg := &Config{
		dict:    make(map[string]any),
		errDict: make(map[string]error),
	}
	if err := parseEnv(cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}

func parseEnv(cfg *Config) error {
	value := reflect.ValueOf(cfg)
	if value.Kind() != reflect.Pointer || value.IsNil() {
		return errors.New("配置不能为空")
	}
	value = value.Elem()
	cfg.parseStruct(value)
	if len(cfg.errDict) > 0 {
		var result strings.Builder
		for key, value := range cfg.errDict {
			result.WriteString(fmt.Sprintf("配置【%s】：%v\n", key, value))
		}
		return errors.New(result.String())
	}
	return nil
}

func (c *Config) String() string {
	var result strings.Builder
	for key, value := range c.dict {
		result.WriteString(fmt.Sprintf("%s=%v\n", key, value))
	}
	return result.String()
}

func (c *Config) parseStruct(value reflect.Value) {
	typ := value.Type()
	for i := range value.NumField() {
		fieldValue := value.Field(i)
		fieldType := typ.Field(i)
		if fieldValue.Kind() == reflect.Struct {
			c.parseStruct(fieldValue)
			continue
		}
		envKey := fieldType.Tag.Get("env")
		if envKey == "" {
			continue
		}
		envValue, ok := os.LookupEnv(envKey)
		if !ok || envValue == "" {
			envValue, ok = fieldType.Tag.Lookup("envDefault")
			if !ok {
				c.errDict[envKey] = errors.New("不存在")
				continue
			}
		}
		if err := setFieldValue(fieldValue, envValue); err != nil {
			c.errDict[envKey] = fmt.Errorf("值【%s】无法转换为【%s】类型", envValue, fieldType.Type)
			continue
		}
		c.dict[envKey] = fieldValue.Interface()
	}
}

func setFieldValue(field reflect.Value, raw string) error {
	switch field.Kind() {
	case reflect.String:
		field.SetString(raw)

	case reflect.Bool:
		value, err := strconv.ParseBool(raw)
		if err != nil {
			return err
		}
		field.SetBool(value)

	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		value, err := strconv.ParseInt(raw, 10, field.Type().Bits())
		if err != nil {
			return err
		}
		field.SetInt(value)

	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64:
		value, err := strconv.ParseUint(raw, 10, field.Type().Bits())
		if err != nil {
			return err
		}
		field.SetUint(value)

	case reflect.Float32, reflect.Float64:
		value, err := strconv.ParseFloat(raw, field.Type().Bits())
		if err != nil {
			return err
		}
		field.SetFloat(value)

	default:
		return fmt.Errorf("类型【%s】不支持", field.Type())
	}

	return nil
}
