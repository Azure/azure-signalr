# Group Management

A group is a collection of connections/users associated with a name. Messages can be sent to all connections/users in a group. Groups are the recommended way to send to a connection/user or multiple connections/users because the groups are managed by the application. A connection/user can be a member of multiple groups. 

> User ID: can be used for multiple connections
>
> Connection ID: unique, one ID can be used for only one connection 

## Content

- [Group Management](#group-management)
  - [Content](#content)
  - [Group management for connection ID](#group-management-for-connection-id)
    - [Adding and removing connection IDs](#adding-and-removing-connection-ids)
      - [Usage](#usage)
  - [Group management for user ID](#group-management-for-user-id)
    - [Adding and removing user IDs](#adding-and-removing-user-ids)
      - [Usage](#usage-1)
    - [Remove user from all groups](#remove-user-from-all-groups)
      - [Usage](#usage-2)
    - [Check user existence in a group](#check-user-existence-in-a-group)
      - [Usage](#usage-3)

SignalR Service allows users/connections to be added/removed to/from groups.

In general, we support 5 methods for group management for all SDK and REST API for Azure SignalR Service:

|  | [Server SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR/) | [Management SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) | [Function Binding](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.SignalRService) | [REST API](https://github.com/Azure/azure-signalr/blob/dev/docs/rest-api.md) |
| --- | --- | --- | --- | --- |
| Add a connection ID to a group | :heavy_check_mark: |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a connection ID from a group | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Add a user ID from a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from all groups | `N/A` |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Check user ID existence in a group | `N/A` |  `N/A` | `N/A` | :heavy_check_mark: |

## Group management for connection ID

To add or remove connection IDs from a group, you call the add or remove methods, and pass in the connection ID and group's name as parameters. Group membership **IS NOT** preserved when a connection ends. The connection needs to rejoin the group when it's re-established.

At the momment, Azure SignalR Service doesn't provide any methods to check connection membership in a group. Therefore, it is app server's responsibility to manage the connection membership.

### Adding and removing connection IDs

#### Usage

* [Server SDK Usage](https://docs.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-3.0#groups-in-signalr)
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md)

## Group management for user ID

To add or remove user IDs from a group, you call the add or remove methods, and pass in the user ID and group's name as parameters. Unlike connection ID, Group membership **IS** preserved when a user disconnects.

Note that user ID can be viewed as a **tag** to one or more connections. If you add a new tag to a group, that means you notify the group that all the group messages should be sent to the connetion with the tag. It is your responsibility to update the tags (user IDs) in a group.

We provides 2 options to clear tags in groups:

1. [Add a user ID to a group with `TTL`](#adding-and-removing-user-ids).
2. [Excipitly remove a user ID from all groups](#remove-user-from-all-groups).

When you can decide the TTL of a user, you can use the [Add a user ID to a group with `TTL`](#adding-and-removing-user-ids) option. 
Otherwise, we suggest you use the [Excipitly remove a user ID from all groups](#remove-user-from-all-groups), only remove the user from all groups when it is neccessary. This option useful when you disconnect all the connections with the specific user ID and also want the specific user leave all the groups it joined before, so that the user won't be in any group when it reconnects.

### Adding and removing user IDs

As for adding a user ID to group, you can specify a TTL, which mean the user ID will be automatically removed for the specific group. If you add user ID to the specific group for multiple times, the TTL will be updated to the latest TTL you specified. To remove the specific user ID from all groups excipitly, please see [Remove user from all groups](#remove-user-from-all-groups) section.

#### Usage

* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#add-a-user-to-a-group)

### Remove user from all groups

Unlike adding a user ID to a group with TTL, you don't have to decide TTL to user-group pair at the time you add a user ID to a group. Removing a user from all groups can be invoke at any time you want. It will remove the specific user ID in all groups.

#### Usage
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#remove-a-user-from-all-groups)

### Check user existence in a group

Sometimes, you need to check whether a user is in a group or not before. For server environment, you are able to manage the user-group membership in your app server, but for serverless environment, you have no place to manage it. You can call "Check user existence in a group" REST API to accomplish it.

#### Usage
* [REST API Usage](../rest-api.md#check-user-existence-in-a-group)