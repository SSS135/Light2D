using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Light2D.Examples
{
    public class MapGenerator : MonoBehaviour
    {
        private struct CollMapPoint
        {
            public float Noise;
            public BlockSetProfile.BlockInfo BlockInfo;
            public BlockSetProfile.BlockType BlockType;

            public CollMapPoint(float noise)
            {
                Noise = noise;
                BlockInfo = null;
                BlockType = BlockSetProfile.BlockType.Empty;
            }
        }

        public BlockSetProfile BlockSet;
        public bool RandomSeed;
        public int Seed;

        public GameObject MeshObjectPrefab;
        public GameObject LightObstaclesPrefab;
        public GameObject AmbientLightPrefab;
        public Transform Container;
        private Camera _mainCamera;
        private float _randFirstAddX;
        private float _randFirstAddY;
        private float _randSecondAddX;
        private float _randSecondAddY;
        private const int ChunkSize = 32;
        private readonly Dictionary<Vector2Int, GameObject> _tiles = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, CollMapPoint> _collMapPoints = new Dictionary<Vector2Int, CollMapPoint>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Color> _lightAbsorptionColors = new List<Color>();
        private readonly List<Color> _lightEmissionColors = new List<Color>();
        private readonly List<Color> _colors = new List<Color>();
        // Created in Awake: MaterialPropertyBlock wraps a native object, and Unity forbids
        // creating those from a field initializer during scene deserialization.
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _mainCamera = Camera.main;

            if (RandomSeed)
            {
                var spawnPoint = Vector2Int.RoundToInt(FindAnyObjectByType<Spacecraft>().MainRigidbody.position);
                do
                {
                    Seed = Random.Range(-9999999, 9999999);
                    InitRandom();
                    _collMapPoints.Clear();
                } while (!IsGoodSpawnPoint(spawnPoint));
            }
            else
            {
                InitRandom();
            }
        }

        void InitRandom()
        {
            var rand = new SimpleRNG(Seed);
            _randFirstAddX = (rand.value - 0.5f) * 1000f;
            _randFirstAddY = (rand.value - 0.5f) * 10000;
            _randSecondAddX = (rand.value - 0.5f) * 1000f;
            _randSecondAddY = (rand.value - 0.5f) * 1000f;
        }

        private void Start()
        {
            for (var index = Container.childCount - 1; index >= 0; index--)
                Destroy(Container.GetChild(index).gameObject);

            StartCoroutine(ChunkCreatorCoroutine());
            StartCoroutine(ChunkDestroyerCoroutine());
        }

        private IEnumerator ChunkCreatorCoroutine()
        {
            bool firstRun = true;
            while (true)
            {
                var camPos = _mainCamera.transform.position;
                var camChunk = Vector2Int.RoundToInt(new Vector2(camPos.x/ChunkSize, camPos.y/ChunkSize));
                var camHalfHeight = Mathf.CeilToInt(_mainCamera.orthographicSize/ChunkSize) + 2;
                var camHalfWidth = Mathf.CeilToInt(_mainCamera.orthographicSize*_mainCamera.aspect/ChunkSize) + 2;

                for (int x = camChunk.x - camHalfWidth; x <= camChunk.x + camHalfWidth; x++)
                {
                    for (int y = camChunk.y - camHalfHeight; y <= camChunk.y + camHalfHeight; y++)
                    {
                        camPos = _mainCamera.transform.position;
                        camChunk = Vector2Int.RoundToInt(new Vector2(camPos.x/ChunkSize, camPos.y/ChunkSize));

                        var chunkPosition = new Vector2Int(x, y);
                        if (_tiles.ContainsKey(chunkPosition))
                            continue;

                        var chunk = GenerateChunk(x, y);
                        _tiles[chunkPosition] = chunk;

                        if (!firstRun)
                            yield return null;
                    }
                    yield return null;
                }
                firstRun = false;
            }
        }

        private IEnumerator ChunkDestroyerCoroutine()
        {
            var removedChunks = new List<Vector2Int>();
            while (true)
            {
                removedChunks.Clear();
                foreach (var chunk in _tiles)
                {
                    var camPos = _mainCamera.transform.position;
                    var camChunk = Vector2Int.RoundToInt(new Vector2(camPos.x/ChunkSize, camPos.y/ChunkSize));
                    var camHalfHeight = Mathf.CeilToInt(_mainCamera.orthographicSize/ChunkSize) + 20;
                    var camHalfWidth = Mathf.CeilToInt(_mainCamera.orthographicSize*_mainCamera.aspect/ChunkSize) + 20;

                    var pos = chunk.Key;
                    if (pos.x < camChunk.x - camHalfWidth || pos.x > camChunk.x + camHalfWidth ||
                        pos.y < camChunk.y - camHalfHeight || pos.y > camChunk.y + camHalfHeight)
                    {
                        removedChunks.Add(pos);
                    }
                }

                yield return null;

                foreach (var pos in removedChunks)
                {
                    var camPos = _mainCamera.transform.position;
                    var camChunk = Vector2Int.RoundToInt(new Vector2(camPos.x/ChunkSize, camPos.y/ChunkSize));
                    var camHalfHeight = Mathf.CeilToInt(_mainCamera.orthographicSize/ChunkSize) + 2;
                    var camHalfWidth = Mathf.CeilToInt(_mainCamera.orthographicSize*_mainCamera.aspect/ChunkSize) + 2;

                    var chunk = _tiles[pos];
                    if (pos.x < camChunk.x - camHalfWidth || pos.x > camChunk.x + camHalfWidth ||
                        pos.y < camChunk.y - camHalfHeight || pos.y > camChunk.y + camHalfHeight)
                    {
                        Destroy(chunk);
                        _tiles.Remove(pos);
                        yield return null;
                    }
                }

                yield return null;
            }
        }

        private GameObject GenerateChunk(int chunkX, int chunkY)
        {
            return GenerateBlocksJoined(chunkX*ChunkSize, chunkY*ChunkSize);
        }

        private GameObject GenerateBlocksJoined(int xOffest, int yOffest)
        {
            var rand = new SimpleRNG(SampleUtilities.Hash(Seed, xOffest, yOffest));

            _vertices.Clear();
            _uvs.Clear();
            _triangles.Clear();
            _lightAbsorptionColors.Clear();
            _lightEmissionColors.Clear();

            var meshObj = Instantiate(MeshObjectPrefab);
            meshObj.name = "Block Mesh { X = " + xOffest/ChunkSize + "; Y = " + yOffest/ChunkSize + " }";
            var meshObjTransform = meshObj.transform;
            meshObjTransform.position = SampleUtilities.WithXY(meshObjTransform.position, xOffest, yOffest);
            meshObjTransform.parent = Container;

            var collMap = new CollMapPoint[ChunkSize, ChunkSize];

            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    collMap[x, y] = GetCollMapPoint(x + xOffest, y + yOffest);
                }
            }

            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    var blockInfo = collMap[x, y].BlockInfo;

                    if (blockInfo.AditionalObjectPrefab != null && blockInfo.AditionalObjectProbability >= rand.value)
                    {
                        var addObj = (GameObject) Instantiate(blockInfo.AditionalObjectPrefab);

                        addObj.transform.parent = meshObjTransform;
                        addObj.transform.localPosition = SampleUtilities.WithXY(
                            blockInfo.AditionalObjectPrefab.transform.position, x + 0.5f, y + 0.5f);
                    }

                    if (blockInfo.SpriteInfo.Length == 0)
                    {
                        Debug.LogError("Sprite Info is broken");
                        continue;
                    }

                    CreatePoint(x, y, blockInfo, false, rand);
                }
            }

            var blockMesh = new Mesh();
            blockMesh.SetVertices(_vertices);
            blockMesh.SetUVs(0, _uvs);
            blockMesh.SetTriangles(_triangles, 0);
            blockMesh.RecalculateBounds();

            var meshFilter = meshObj.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = blockMesh;
            meshObj.AddComponent<GeneratedMeshOwner>().Mesh = blockMesh;

            var meshRenderer = meshObj.GetComponent<MeshRenderer>();
            var texture = FindBlockTexture();
            _propertyBlock.SetTexture("_MainTex", texture);
            meshRenderer.SetPropertyBlock(_propertyBlock);

            for (int x = 0; x < ChunkSize; x++)
            {
                var yStart = 0;
                for (int y = 0; y < ChunkSize; y++)
                {
                    if (collMap[x, y].BlockInfo.BlockType != BlockSetProfile.BlockType.CollidingWall)
                    {
                        if (y - yStart > 0)
                        {
                            var obj = new GameObject();
                            obj.layer = meshObj.layer;
                            obj.transform.parent = meshObjTransform;
                            obj.transform.localPosition = new Vector3(x, 0);
                            obj.name = "Collider x = " + x;
                            var coll = obj.AddComponent<BoxCollider2D>();
                            coll.size = new Vector2(1, (y - yStart));
                            coll.offset = new Vector2(0.5f, yStart + coll.size.y/2f);
                        }
                        yStart = y + 1;
                    }
                }
                if (ChunkSize - yStart > 0)
                {
                    var obj = new GameObject();
                    obj.layer = meshObj.layer;
                    obj.transform.parent = meshObjTransform;
                    obj.transform.localPosition = new Vector3(x, 0);
                    obj.name = "Collider x = " + x;
                    var coll = obj.AddComponent<BoxCollider2D>();
                    coll.size = new Vector2(1, (ChunkSize - yStart));
                    coll.offset = new Vector2(0.5f, yStart + coll.size.y/2f);
                }
            }

            var lightObstaclesObject = Instantiate(LightObstaclesPrefab);
            lightObstaclesObject.transform.parent = meshObjTransform;
            lightObstaclesObject.transform.localPosition = Vector3.zero;
            var lightObstaclesMeshFilter = lightObstaclesObject.GetComponent<MeshFilter>();
            var obstacleMesh = ChunkMeshFromColors(_lightAbsorptionColors);
            lightObstaclesMeshFilter.sharedMesh = obstacleMesh;
            lightObstaclesObject.AddComponent<GeneratedMeshOwner>().Mesh = obstacleMesh;

            var ambientLightObject = Instantiate(AmbientLightPrefab);
            ambientLightObject.transform.parent = meshObjTransform;
            ambientLightObject.transform.localPosition = Vector3.zero;
            var ambientLightMeshFilter = ambientLightObject.GetComponent<MeshFilter>();
            var ambientMesh = ChunkMeshFromColors(_lightEmissionColors);
            ambientLightMeshFilter.sharedMesh = ambientMesh;
            ambientLightObject.AddComponent<GeneratedMeshOwner>().Mesh = ambientMesh;

            return meshObj;
        }

        private Texture FindBlockTexture()
        {
            foreach (var blockInfo in BlockSet.BlockInfos)
            {
                foreach (var sprite in blockInfo.SpriteInfo)
                {
                    if (sprite != null)
                        return sprite.texture;
                }
            }

            throw new InvalidOperationException("The block set must contain at least one sprite.");
        }

        private Mesh ChunkMeshFromColors(List<Color> colors)
        {
            _vertices.Clear();
            _triangles.Clear();
            _colors.Clear();

            const float add = 0;

            for (int x = 0; x < ChunkSize; x++)
            {
                var yStart = 0;
                var startC = colors[x*ChunkSize];
                for (int y = 0; y < ChunkSize; y++)
                {
                    var currC = colors[y + x*ChunkSize];
                    if (currC.r != startC.r || currC.g != startC.g || currC.b != startC.b || currC.a != startC.a)
                    {
                        var startVert = _vertices.Count;

                        _vertices.Add(new Vector3(x - add, yStart - add, 0));
                        _vertices.Add(new Vector3(x + 1 + add, yStart - add, 0));
                        _vertices.Add(new Vector3(x - add, y + add, 0));
                        _vertices.Add(new Vector3(x + 1 + add, y + add, 0));

                        _triangles.Add(startVert + 2);
                        _triangles.Add(startVert + 1);
                        _triangles.Add(startVert);
                        _triangles.Add(startVert + 1);
                        _triangles.Add(startVert + 2);
                        _triangles.Add(startVert + 3);

                        for (int i = 0; i < 4; i++)
                            _colors.Add(startC);

                        startC = currC;

                        yStart = y;
                    }
                }
                if (ChunkSize - yStart > 0)
                {
                    var startVert = _vertices.Count;

                    _vertices.Add(new Vector3(x - add, yStart - add, 0));
                    _vertices.Add(new Vector3(x + 1 + add, yStart - add, 0));
                    _vertices.Add(new Vector3(x - add, ChunkSize + add, 0));
                    _vertices.Add(new Vector3(x + 1 + add, ChunkSize + add, 0));

                    _triangles.Add(startVert + 2);
                    _triangles.Add(startVert + 1);
                    _triangles.Add(startVert);
                    _triangles.Add(startVert + 1);
                    _triangles.Add(startVert + 2);
                    _triangles.Add(startVert + 3);

                    for (int i = 0; i < 4; i++)
                        _colors.Add(startC);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_triangles, 0);
            mesh.SetColors(_colors);

            return mesh;
        }

        private void CreatePoint(int x, int y, BlockSetProfile.BlockInfo blockInfo,
            bool noLightEffects, SimpleRNG rand, BlockSetProfile.BlockType? isColliding = null)
        {
            var sprite = blockInfo.SpriteInfo
                .RandomElement(ti => 1, rand);

            if (sprite == null)
            {
                Debug.LogError("Tiling info is broken");
                return;
            }

            CreatePoint(x, y, sprite, blockInfo, isColliding == null ? blockInfo.BlockType : isColliding.Value,
                noLightEffects);
        }

        private void CreatePoint(int x, int y, Sprite sprite, BlockSetProfile.BlockInfo blockInfo,
            BlockSetProfile.BlockType isColliding, bool noLightEffect)
        {
            var textureSize = new Vector2(sprite.texture.width, sprite.texture.height);
            var uvRect = sprite.textureRect;

            var startVert = _vertices.Count;
            const float add = 0.01f;
            float z = isColliding == BlockSetProfile.BlockType.CollidingWall ? 0 : 5;
            _vertices.Add(new Vector3(x - add, y - add, z));
            _vertices.Add(new Vector3(x + 1 + add, y - add, z));
            _vertices.Add(new Vector3(x - add, y + 1 + add, z));
            _vertices.Add(new Vector3(x + 1 + add, y + 1 + add, z));

            _uvs.Add(new Vector2(uvRect.xMin/textureSize.x, uvRect.yMin/textureSize.y)); // 0, 0
            _uvs.Add(new Vector2(uvRect.xMax/textureSize.x, uvRect.yMin/textureSize.y)); // 1, 0
            _uvs.Add(new Vector2(uvRect.xMin/textureSize.x, uvRect.yMax/textureSize.y)); // 0, 1
            _uvs.Add(new Vector2(uvRect.xMax/textureSize.x, uvRect.yMax/textureSize.y)); // 1, 1

            _triangles.Add(startVert + 2);
            _triangles.Add(startVert + 1);
            _triangles.Add(startVert);
            _triangles.Add(startVert + 1);
            _triangles.Add(startVert + 2);
            _triangles.Add(startVert + 3);

            _lightAbsorptionColors.Add(noLightEffect ? new Color() : blockInfo.LightAbsorption);
            _lightEmissionColors.Add(noLightEffect ? new Color() : blockInfo.LightEmission);
        }

        private CollMapPoint GetCollMapPoint(int x, int y, BlockSetProfile.BlockType? colliding = null)
        {
            CollMapPoint cachedPoint;
            var position = new Vector2Int(x, y);
            if (_collMapPoints.TryGetValue(position, out cachedPoint) &&
                (colliding == null || colliding.Value == cachedPoint.BlockType))
            {
                return cachedPoint;
            }

            var noise = GetNoise(x, y);

            var rand = new SimpleRNG(SampleUtilities.Hash(Seed, x, y));
            var blockInfo = SelectBlockInfo(noise, colliding, rand);
            if (blockInfo == null && colliding != null)
                blockInfo = SelectBlockInfo(null, colliding, rand);

            if (blockInfo == null)
            {
                Debug.LogError("No matching blocks found");
                return default(CollMapPoint);
            }

            var point = new CollMapPoint(noise) {BlockInfo = blockInfo, BlockType = blockInfo.BlockType};
            _collMapPoints[position] = point;
            return point;
        }

        private BlockSetProfile.BlockInfo SelectBlockInfo(float? noise, BlockSetProfile.BlockType? blockType, SimpleRNG rand)
        {
            BlockSetProfile.BlockInfo selected = null;
            var totalWeight = 0;
            foreach (var candidate in BlockSet.BlockInfos)
            {
                if (noise != null && (candidate.MinNoise > noise.Value || candidate.MaxNoise < noise.Value) ||
                    blockType != null && candidate.BlockType != blockType.Value)
                    continue;

                var weight = Mathf.Max(0, candidate.Weight);
                totalWeight += weight;
                if (weight > 0 && rand.Range(totalWeight) >= totalWeight - weight)
                    selected = candidate;
            }

            return selected;
        }

        float GetNoise(int x, int y)
        {
            var noise1 = (Noise.Generate(
                x * BlockSet.FirstNoiseScale + _randFirstAddX,
                y * BlockSet.FirstNoiseScale + _randFirstAddY) - 0.5f) * 2f;
            var noise2 = (Noise.Generate(
                x * BlockSet.SecondNoiseScale + _randSecondAddX,
                y * BlockSet.SecondNoiseScale + _randSecondAddY) - 0.5f) * 2f;

            var noise = Mathf.Clamp01((noise1 + noise2 * BlockSet.SecondNoiseMul) / 2f + 0.5f);

            return noise;
        }

        bool IsGoodSpawnPoint(Vector2Int point)
        {
            for (int x = point.x - 10; x <= point.x + 10; x++)
            {
                for (int y = point.y - 10; y < point.y + 10; y++)
                {
                    if (GetCollMapPoint(x, y).BlockType == BlockSetProfile.BlockType.CollidingWall)
                        return false;
                }
            }
            return true;
        }

        [ContextMenu("Recreate map")]
        private void RecreateMap()
        {
            foreach (var tile in _tiles)
            {
                Destroy(tile.Value);
            }
            _tiles.Clear();
        }

    }

    internal sealed class GeneratedMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh != null)
                Destroy(Mesh);
        }
    }
}
