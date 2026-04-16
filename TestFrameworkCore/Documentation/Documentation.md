# Core-Idea

A System of Artifacts and Steps.
Combined in one "Timeline" to order the Operations.
The Core is the Engine it should only provide a scaffold so other "extensions" can implement the specific logic to interface with the Core.

## Artifacts

Data that is emitted remotely. This can take any Shape as long it is Data that is stored "persistently".
A key feature of Artifacts is that is should be cleanable - so to restore the original state of the Environment.
Artifacts exist for as max the duration of the Timeline.

### ArtifactDescriber
An ArtifactDescriber should only store Information about any Artifact of this kind. (How to Create/ Cleanup/ ...)

### ArtifactReference
This will Identify the Artifact. It should make sure that the Reference is Unique to not remove anything it should not.

### ArtifactInstance
This will store Information relevant to a specific run. Its job is to track the State/ Artifact it self/ Artifact Data

### ArtifactData
It will store the Data for a specific Artifact Kind. It should be a raw and Simple Data representation to allow for Versioning. Like Bytes for a Blob or File.

### Relation
```
RunInstance
\- ArtifactInstance
   |- Artifact (Static description)
   |  |- Setup
   |  |- Remove
   |  ...
   |- ArtifactReference (Data about the source referance)
   |  \- Refs
   \- ArtifactData (Data that got fetched or provided)
      \- Data
```

## Steps

Actions that can be taken. Be it to employ a new Artifact, to Trigger external or internal components or just to control the flow of the Timeline.
Steps are independent in nature so to not be dependent on the Order of the Timeline.

The Step should only store Information about how to Execute it and a set of run-independent settings.

A Step should be able to be Retried.

### StepInstance
This will store Information relevant to a specific run. Its job is to track the State/ ...
Should store Information about the Retry.

## Variables

A way to transfer Value between Steps. Variables should not interfere with const Values - so to not worsen the user experience.

### ResolvableVariable
An mechanic to resolve Variables at runtime. It should store a way to resolve the Value it is Referring to.

#### VariableReference
A version of the ResolvableVariable it will get the Value set prior.

#### VariableConst
A version of the ResolvableVariable it will provide a constant Value.

## Timeline

A collection of Steps. With a fixed order so to guarantee persistence. Every Timeline-"run" has to run independent of other runs of the same Timeline.
A Timeline-run should provide mechanisms to deal with failure and problems of the same kind. It must declare this to the User.

# Flow

## Timeline Setup

The Setup is via Builder. The builder should provide a fluent API to support a more friendly way of understanding the flow of the Timeline.
The Builder is more focused on providing more high level representation of steps.

### Actions the Builder should Support:
- Trigger Actions - a way to trigger external actions
- Event listener - a way to wait for external events
- Setup Artifacts - a way to employ new Artifacts
- Remove Artifacts - a way to remove employed Artifacts
- Register Artifacts - a way to register employed Artifacts which have been created by an external source

## Timeline run

The runner should resemble a State-Machine. It will execute Step after step. It is responsible for respecting common settings made for the step as well as handling any errors - either by escalating them or ignoring them.
The runner will handle any Run-Store interactions (Vars/ Artifacts).

A run consist of two stages:
- Main-stage - where any steps are executed. (like Artifact-Setup/ Trigger/ ...)
- Cleanup-stage - where any Artifacts are cleaned

# Future
Stuff that is not in this iteration

- [ ] Defer - a system to execute Actions after the cleanup
- [ ] Checkpoints - a system to track if checkpoints are Hit