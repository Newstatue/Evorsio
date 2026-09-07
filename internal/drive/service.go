package drive

type Service struct {
	r *Repository
}

func NewService(r *Repository) *Service {
	return &Service{
		r: r,
	}
}
