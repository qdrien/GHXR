# GHXRTable - Grasshopper Plugin ![Logo GHXRTableGH](GHXRTableGH/Resources/logo.png "Logo GHXRTableGH")

Connects Grasshopper to GHXRTable modules (or potentially other software) via MQTT. 
Sends meshes and parameters and receives parameter value updates.

By default, the plugin uses respectively the `GHXRTable/Meshes`, `GHXRTable/ParameterShare` and `GHXRTable/ParameterControl` topics to do so, on the `localhost` broker.

In order to use the plugin, the whole output of the bin/ folder must be loaded in GH.
The _GrasshopperDeveloperSettings Rhino command can be used to do so (to avoid copying the files to the default location for GH libraries).


##Data format
####Meshes:
```json
[
    {
        "vertices":[
            {
                "x":1.0,
                "y":1.0,
                "z":1.0
            },
            {...} //other vertices
        ],
        "uvs":[
            {
                "x":1.0,
                "y":1.0
            },
            {...} //other uvs
        ],
        "normals":[
            {
                "x":1.0,
                "y":1.0,
                "z":1.0
            },
            {...} //other normals
        ],
        "faces":[
            {
                "isQuad":true,
                "a":1,
                "b":1,
                "c":1,
                "d":1
            },
            {...} //other faces
        ],
    },
    {...} //other meshes
]
```
####Parameters:
```json
[
    {
        "Type":"toggle",
        "Name":"",          //can be omitted when sending 'ParameterControl' updates
        "NickName":"",      //can be omitted when sending 'ParameterControl' updates
        "Guid":"",
        "Value":true
    },
    {
        "Type":"slider",
        "Name":"",          //can be omitted when sending 'ParameterControl' updates
        "NickName":"",      //can be omitted when sending 'ParameterControl' updates
        "Guid":"",
        "Value":1.0,
        "Accuracy":1,       //can be omitted when sending 'ParameterControl' updates
        "Min":1.0,          //can be omitted when sending 'ParameterControl' updates
        "Max":1.0,          //can be omitted when sending 'ParameterControl' updates
        "Epsilon":1.0,      //can be omitted when sending 'ParameterControl' updates
        "DecimalPlaces":1   //can be omitted when sending 'ParameterControl' updates
    },
    {...} //other parameters
]
```


---
####TODO list:
- Refactor the code to split the behaviour into more classes and methods.
- Support more parameter types?
- Find a solution to prevent the warnings when expiring the solution from within the SolveInstance method
    - See links
- License
- Multiple logging outputs? (e.g. one for each topic to store the last messages received/sent)
- Enable SSL?
- Optimise data format? (e.g. FlatBuffers instead of JSON)
    - Probably not worth it since the bottleneck will still likely be GH's re-computation time and it would make the code slightly more complex (in addition to rendering the data unreadable by humans). 