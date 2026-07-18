<!-- GENERATED from Docs~/WikiSource + tool schema on domain reload. Do not edit; edit Docs~/WikiSource/particle.system-get.md. -->

---
name: particle-system-get
description: Get detailed information about a ParticleSystem component on a GameObject. Returns particle system state and optionally serialized data for each module. Use the boolean flags to request specific modules. Use this to inspect ParticleSystem data before modifying it.
---

# ParticleSystem / Get

Get detailed information about a ParticleSystem component on a GameObject. Returns particle system state and optionally serialized data for each module. Use the boolean flags to request specific modules. Use this to inspect ParticleSystem data before modifying it.

## How to Call

Invoke this method through your MCP client's `call` tool. Supply `method` plus `args` to execute; omit `args` (or pass nothing) to fetch this wiki page instead.

### Execute

```json
{ "method": "particle.system-get", "args": {
  "gameObjectRef": "string_value",
  "componentRef": "string_value",
  "includeMain": false,
  "includeEmission": false,
  "includeShape": false,
  "includeVelocityOverLifetime": false,
  "includeLimitVelocityOverLifetime": false,
  "includeInheritVelocity": false,
  "includeLifetimeByEmitterSpeed": false,
  "includeForceOverLifetime": false,
  "includeColorOverLifetime": false,
  "includeColorBySpeed": false,
  "includeSizeOverLifetime": false,
  "includeSizeBySpeed": false,
  "includeRotationOverLifetime": false,
  "includeRotationBySpeed": false,
  "includeExternalForces": false,
  "includeNoise": false,
  "includeCollision": false,
  "includeTrigger": false,
  "includeSubEmitters": false,
  "includeTextureSheetAnimation": false,
  "includeLights": false,
  "includeTrails": false,
  "includeCustomData": false,
  "includeRenderer": false,
  "includeAll": false,
  "deepSerialization": false
} }
```

### Discovery (read this wiki)

Omit `args` to fetch this wiki page instead of executing:
```json
{ "method": "particle.system-get" }
```

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Reference to the GameObject containing the ParticleSystem component. |
| `componentRef` | `any` | No | Optional reference to a specific ParticleSystem component if the GameObject has multiple. If not provided, uses the first ParticleSystem found. |
| `includeMain` | `boolean` | No | Include Main module data (duration, looping, prewarm, startDelay, startLifetime, startSpeed, startSize, startRotation, startColor, gravityModifier, simulationSpace, scalingMode, playOnAwake, maxParticles, etc.). |
| `includeEmission` | `boolean` | No | Include Emission module data (rateOverTime, rateOverDistance, bursts). |
| `includeShape` | `boolean` | No | Include Shape module data (shapeType, radius, angle, arc, position, rotation, scale, mesh, texture, etc.). |
| `includeVelocityOverLifetime` | `boolean` | No | Include Velocity over Lifetime module data (x, y, z, space, orbital, radial, speedModifier). |
| `includeLimitVelocityOverLifetime` | `boolean` | No | Include Limit Velocity over Lifetime module data (limit, dampen, separateAxes, drag). |
| `includeInheritVelocity` | `boolean` | No | Include Inherit Velocity module data (mode, curve). |
| `includeLifetimeByEmitterSpeed` | `boolean` | No | Include Lifetime by Emitter Speed module data (curve, range). |
| `includeForceOverLifetime` | `boolean` | No | Include Force over Lifetime module data (x, y, z, space, randomized). |
| `includeColorOverLifetime` | `boolean` | No | Include Color over Lifetime module data (color gradient). |
| `includeColorBySpeed` | `boolean` | No | Include Color by Speed module data (color, range). |
| `includeSizeOverLifetime` | `boolean` | No | Include Size over Lifetime module data (size curve, separateAxes). |
| `includeSizeBySpeed` | `boolean` | No | Include Size by Speed module data (size, range). |
| `includeRotationOverLifetime` | `boolean` | No | Include Rotation over Lifetime module data (angular velocity, separateAxes). |
| `includeRotationBySpeed` | `boolean` | No | Include Rotation by Speed module data (angular velocity, range). |
| `includeExternalForces` | `boolean` | No | Include External Forces module data (multiplier, influenceFilter). |
| `includeNoise` | `boolean` | No | Include Noise module data (strength, frequency, scrollSpeed, damping, octaves, quality, remap). |
| `includeCollision` | `boolean` | No | Include Collision module data (type, mode, planes, dampen, bounce, lifetimeLoss). |
| `includeTrigger` | `boolean` | No | Include Trigger module data (inside, outside, enter, exit actions). |
| `includeSubEmitters` | `boolean` | No | Include Sub Emitters module data (birth, collision, death, trigger, manual emitters). |
| `includeTextureSheetAnimation` | `boolean` | No | Include Texture Sheet Animation module data (mode, tiles, animation, frameOverTime). |
| `includeLights` | `boolean` | No | Include Lights module data (ratio, light, color, range, intensity). |
| `includeTrails` | `boolean` | No | Include Trails module data (mode, ratio, lifetime, width, color). |
| `includeCustomData` | `boolean` | No | Include Custom Data module data (modes, vectors, colors). |
| `includeRenderer` | `boolean` | No | Include Renderer module data (renderMode, material, sortMode, alignment, shadows). |
| `includeAll` | `boolean` | No | Include ALL modules data. Overrides individual flags. |
| `deepSerialization` | `boolean` | No | Performs deep serialization including all nested objects. Otherwise, only serializes top-level members. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/EA.GameObjectRef"
    },
    "componentRef": {
      "$ref": "#/$defs/EA.ComponentRef"
    },
    "includeMain": {
      "type": "boolean"
    },
    "includeEmission": {
      "type": "boolean"
    },
    "includeShape": {
      "type": "boolean"
    },
    "includeVelocityOverLifetime": {
      "type": "boolean"
    },
    "includeLimitVelocityOverLifetime": {
      "type": "boolean"
    },
    "includeInheritVelocity": {
      "type": "boolean"
    },
    "includeLifetimeByEmitterSpeed": {
      "type": "boolean"
    },
    "includeForceOverLifetime": {
      "type": "boolean"
    },
    "includeColorOverLifetime": {
      "type": "boolean"
    },
    "includeColorBySpeed": {
      "type": "boolean"
    },
    "includeSizeOverLifetime": {
      "type": "boolean"
    },
    "includeSizeBySpeed": {
      "type": "boolean"
    },
    "includeRotationOverLifetime": {
      "type": "boolean"
    },
    "includeRotationBySpeed": {
      "type": "boolean"
    },
    "includeExternalForces": {
      "type": "boolean"
    },
    "includeNoise": {
      "type": "boolean"
    },
    "includeCollision": {
      "type": "boolean"
    },
    "includeTrigger": {
      "type": "boolean"
    },
    "includeSubEmitters": {
      "type": "boolean"
    },
    "includeTextureSheetAnimation": {
      "type": "boolean"
    },
    "includeLights": {
      "type": "boolean"
    },
    "includeTrails": {
      "type": "boolean"
    },
    "includeCustomData": {
      "type": "boolean"
    },
    "includeRenderer": {
      "type": "boolean"
    },
    "includeAll": {
      "type": "boolean"
    },
    "deepSerialization": {
      "type": "boolean"
    }
  },
  "$defs": {
    "UnityEngine.EntityId": {
      "type": "integer"
    },
    "System.Type": {
      "type": "string"
    },
    "EA.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If it is \u00270\u0027 and \u0027path\u0027, \u0027name\u0027, \u0027assetPath\u0027 and \u0027assetGuid\u0027 is not provided, empty or null, then it will be used as \u0027null\u0027. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample \u0027character/hand/finger/particle\u0027. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with \u0027Assets/\u0027"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Find GameObject in opened Prefab or in the active Scene."
    },
    "EA.ComponentRef": {
      "type": "object",
      "properties": {
        "index": {
          "type": "integer",
          "description": "Component \u0027index\u0027 attached to a gameObject. The first index is \u00270\u0027 and that is usually Transform or RectTransform. Priority: 2. Default value is -1."
        },
        "typeName": {
          "type": "string",
          "description": "Component type full name. Sample \u0027UnityEngine.Transform\u0027. If the gameObject has two components of the same type, the output component is unpredictable. Priority: 3. Default value is null."
        },
        "instanceID": {
          "$ref": "#/$defs/UnityEngine.EntityId",
          "description": "instanceID of the UnityEngine.Object. If this is \u00270\u0027, then it will be used as \u0027null\u0027."
        }
      },
      "required": [
        "index",
        "instanceID"
      ],
      "description": "Component reference. Used to find a Component at GameObject."
    }
  },
  "required": [
    "gameObjectRef"
  ]
}
```

## Output

This tool does not return a structured schema; results are delivered as JSON-RPC result content.

