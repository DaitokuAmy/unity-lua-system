local message = "not set"

function add(left, right)
    return left + right
end

function multiply(left, right)
    return left * right
end

function greet(name)
    return "Hello, " .. name .. "!"
end

function is_even(value)
    return value % 2 == 0
end

function sum_array(values)
    local result = 0
    for index = 1, #values do
        result = result + values[index]
    end
    return result
end

function set_message(value)
    message = value
end

function get_message()
    return message
end

function run_csharp_binding_sample()
    local sum = sample_static.add(7, 8)
    local decorated = sample_static.decorate("called from Lua")
    local instance_message = sample_instance.create_message("hello")

    local player = sample_instance.get_player()
    local player_name = player:get_name()
    local health_before = player:get_health()
    local health_after = player:heal(15)

    return string.format(
        "%s | sum=%d | %s | player=%s, health=%d->%d",
        decorated,
        sum,
        instance_message,
        player_name,
        health_before,
        health_after)
end

function run_csharp_array_sample()
    local lua_table_sum = sample_static.sum({ 4, 5, 6 })
    local csharp_array = sample_static.create_values()
    return lua_table_sum + sum_array(csharp_array)
end

function run_csharp_async_sample()
    local result = sample_instance.load_message_async("awaited by Lua")
    return result .. " -> Lua resumed"
end

function run_csharp_unitask_sample()
    local result = sample_instance.load_unitask_message_async("awaited by Lua")
    return result .. " -> Lua resumed"
end

function run_csharp_coroutine_sample()
    sample_instance.run_controlled_operation()
    return "C# IEnumerator completed -> Lua resumed"
end
