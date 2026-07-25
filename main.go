package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"math/rand/v2"
	"os"
	"strconv"
	"time"
)

const (
	Wall          = '#'
	Door          = 'D'
	DoorCandidate = '#'

	MaxDoorPercent = 40
	DoorChance     = 50
)

type Room struct {
	Id      int
	Name    string
	Ability string
	Layout  []string
}

type Map struct {
	Seed       [32]byte
	PosXPlayer int
	PosYPlayer int
	Rooms      []Room
}

func main() {
	var world Map

	copy(world.Seed[:], []byte(strconv.Itoa(time.Now().Nanosecond())))

	rnd := rand.New(rand.NewChaCha8(world.Seed))

	newRoom, err := world.FirstGeneration(rnd)
	if err != nil {
		fmt.Println("Ошибка генерации комнаты:", err)
		return
	}

	world.Rooms = append(world.Rooms, newRoom)

	fmt.Printf(
		"Создана комната: %s, всего комнат: %d\n",
		newRoom.Name,
		len(world.Rooms),
	)

	for _, row := range newRoom.Layout {
		fmt.Println(row)
	}
}

func (m *Map) FirstGeneration(rnd *rand.Rand) (Room, error) {
	info, err := os.ReadFile("./rooms/test_room.json")
	if err != nil {
		return Room{}, fmt.Errorf(
			"не удалось прочитать test_room.json: %w",
			err,
		)
	}

	var room Room

	if err := json.Unmarshal(info, &room); err != nil {
		return Room{}, fmt.Errorf(
			"не удалось разобрать JSON комнаты: %w",
			err,
		)
	}

	generatedLayout, err := GenerateDoors(room.Layout, rnd)
	if err != nil {
		return Room{}, fmt.Errorf(
			"не удалось сгенерировать двери: %w",
			err,
		)
	}

	room.Layout = generatedLayout

	return room, nil
}

func GenerateDoors(
	room []string,
	rnd *rand.Rand,
) ([]string, error) {
	if len(room) < 3 {
		return nil, errors.New(
			"высота комнаты должна быть не меньше 3",
		)
	}

	width := len(room[0])

	if width < 3 {
		return nil, errors.New(
			"ширина комнаты должна быть не меньше 3",
		)
	}

	for i, row := range room {
		if len(row) != width {
			return nil, fmt.Errorf(
				"строка %d имеет длину %d, ожидалось %d",
				i,
				len(row),
				width,
			)
		}
	}

	grid := make([][]byte, len(room))

	for i, row := range room {
		grid[i] = []byte(row)
	}

	height := len(grid)

	generateHorizontalDoors(grid, 0, rnd)
	generateHorizontalDoors(grid, height-1, rnd)

	generateVerticalDoors(grid, 0, rnd)
	generateVerticalDoors(grid, width-1, rnd)

	/*
		Это имеет смысл, когда DoorCandidate = 'X'.

		При DoorCandidate = '#' код просто заменяет
		оставшиеся # на #, то есть ничего не меняет.
	*/
	for row := range grid {
		for column := range grid[row] {
			if grid[row][column] == DoorCandidate {
				grid[row][column] = Wall
			}
		}
	}

	result := make([]string, height)

	for i := range grid {
		result[i] = string(grid[i])
	}

	return result, nil
}

// generateHorizontalDoors обрабатывает верхнюю или нижнюю стену.
func generateHorizontalDoors(
	grid [][]byte,
	row int,
	rnd *rand.Rand,
) {
	width := len(grid[row])

	wallLength := width - 2
	maxDoors := calculateMaxDoors(wallLength)

	candidates := make([]int, 0)
	doorCount := 0

	for column := 1; column < width-1; column++ {
		switch grid[row][column] {
		case DoorCandidate:
			candidates = append(candidates, column)

		case Door:
			// Учитываем двери, которые уже были в шаблоне.
			doorCount++
		}
	}

	shuffle(candidates, rnd)

	for _, column := range candidates {
		if doorCount >= maxDoors {
			break
		}

		if !canPlaceHorizontalDoor(grid, row, column) {
			continue
		}

		if rnd.IntN(100) >= DoorChance {
			continue
		}

		grid[row][column] = Door
		doorCount++
	}
}

// generateVerticalDoors обрабатывает левую или правую стену.
func generateVerticalDoors(
	grid [][]byte,
	column int,
	rnd *rand.Rand,
) {
	height := len(grid)

	wallLength := height - 2
	maxDoors := calculateMaxDoors(wallLength)

	candidates := make([]int, 0)
	doorCount := 0

	for row := 1; row < height-1; row++ {
		switch grid[row][column] {
		case DoorCandidate:
			candidates = append(candidates, row)

		case Door:
			doorCount++
		}
	}

	shuffle(candidates, rnd)

	for _, row := range candidates {
		if doorCount >= maxDoors {
			break
		}

		if !canPlaceVerticalDoor(grid, row, column) {
			continue
		}

		if rnd.IntN(100) >= DoorChance {
			continue
		}

		grid[row][column] = Door
		doorCount++
	}
}

// canPlaceHorizontalDoor проверяет соседей слева и справа.
func canPlaceHorizontalDoor(
	grid [][]byte,
	row int,
	column int,
) bool {
	if grid[row][column] != DoorCandidate {
		return false
	}

	left := grid[row][column-1]
	right := grid[row][column+1]

	return left != Door && right != Door
}

// canPlaceVerticalDoor проверяет соседей сверху и снизу.
func canPlaceVerticalDoor(
	grid [][]byte,
	row int,
	column int,
) bool {
	if grid[row][column] != DoorCandidate {
		return false
	}

	top := grid[row-1][column]
	bottom := grid[row+1][column]

	return top != Door && bottom != Door
}

// calculateMaxDoors считает максимум дверей на одной стороне.
func calculateMaxDoors(wallLength int) int {
	maxDoors := wallLength * MaxDoorPercent / 100

	if wallLength > 0 && maxDoors == 0 {
		maxDoors = 1
	}

	return maxDoors
}

// shuffle перемешивает позиции кандидатов.
func shuffle(values []int, rnd *rand.Rand) {
	for i := len(values) - 1; i > 0; i-- {
		j := rnd.IntN(i + 1)
		values[i], values[j] = values[j], values[i]
	}
}
