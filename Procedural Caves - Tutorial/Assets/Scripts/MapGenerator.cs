using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

    public int width;
    public int height;
    public string seed;
    public bool useRandomSeed;

    [Range(0, 100)]
    public int randomFillPercent;

    int[,] map;

    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            GenerateMap();
    }

    void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for(int i = 0; i < 5; i++)
        {
            SmoothMap();
        }

        ProcessMap();

        int borderSize = 1;
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];


        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1);
    }

    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);
        int wallThresholdSize = 50;

        foreach(List<Coord> wr in wallRegions)
        {
            if (wr.Count < wallThresholdSize)
            {
                foreach(Coord c in wr)
                {
                    map[c.tileX, c.tileY] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(0);
        int roomThresholdSize = 50;
        List<Room> survivingRooms = new List<Room>();

        foreach (List<Coord> rr in roomRegions)
        {
            if (rr.Count < roomThresholdSize)
            {
                foreach (Coord c in rr)
                {
                    map[c.tileX, c.tileY] = 1;
                }
            }
            else
            {
                survivingRooms.Add(new Room(rr, map));
            }
        }

        ConnectClosestRooms(survivingRooms);
    }

    void ConnectClosestRooms(List<Room> allRooms)
    {
        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in allRooms)
        {
            possibleConnectionFound = false;
            foreach (Room roomB in allRooms)
            {
                if (roomA == roomB)
                    continue;
                if (roomA.isConnected(roomB))
                {
                    possibleConnectionFound = false;
                    break;
                }

                for(int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for(int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];

                        int distanceBetweenRooms = (int) (Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY,2));

                        if(distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }

            if (possibleConnectionFound)
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
        }
    }

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);
        //Debug.DrawLine(CoordToWorldPoint(tileA), CoordToWorldPoint(tileB), Color.green, 100);

        List<Coord> line = GetLine(tileA, tileB);

        foreach(Coord c in line)
        {
            DrawCircle(c, 2);
        }
    }

    void DrawCircle(Coord c, int r)
    {
        for(int x = -r; x <= r; x++)
        {
            for(int y = -r; y<=r; y++)
            {
                if(x*x + y*y <= r * r)
                {
                    int realX = c.tileX + x;
                    int realY = c.tileY + y;
                    if(IsInMapRange(realX, realY))
                    {
                        map[realX, realY] = 0;
                    }
                }
            }
        }
    }

    List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - x;
        int dy = to.tileY - y;

        bool inverted = false;

        int step = System.Math.Sign(dx);
        int gradientStep = System.Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if(longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = System.Math.Sign(dy);
            gradientStep = System.Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for(int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
                y += step;
            else
                x += step;

            gradientAccumulation += shortest;

            if(gradientAccumulation >= longest)
            {
                if (inverted)
                    x += gradientStep;
                else
                    y += gradientStep;

                 gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 CoordToWorldPoint(Coord tile)
    {
        return new Vector3(-width / 2 + 0.5f + tile.tileX, 2, -height / 2 + .5f + tile.tileY);
    }

    List<List<Coord>> GetRegions (int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];

        for(int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach(Coord c in newRegion)
                    {
                        mapFlags[c.tileX, c.tileY] = 1;
                    }
                }
            }
        }
        return regions;
    }

    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while(queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for(int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for(int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if(IsInMapRange(x, y) && (y == tile.tileY || x == tile.tileX))
                    {
                        if(mapFlags[x,y] == 0 && map[x,y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }
        return tiles;
    }

    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        System.Random prng = new System.Random(seed.GetHashCode());

        for(int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (prng.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if (neighbourWallTiles > 4)
                    map[x, y] = 1;
                else if (neighbourWallTiles < 4)
                    map[x, y] = 0;
            }
        }
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if(IsInMapRange(neighbourX, neighbourY)) { 
                    if(neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }

        return wallCount;
    }

    struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }

    class Room
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;

        public Room()
        {

        }

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = roomTiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();

            foreach (Coord c in tiles)
            {
                for (int x = c.tileX - 1; x <= c.tileX + 1; x++) {
                    for(int y = c.tileY - 1; y <= c.tileY + 1; y++)
                    {
                        if(x == c.tileX || y == c.tileY)
                        {
                            if(map[x,y] == 1)
                            {
                                edgeTiles.Add(c);
                            }
                        }
                    }
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB)
        {
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        public bool isConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }
    }
}
