namespace CreaturesReborn.Godot.BrainGpu;

internal static class BrainGpuShaderLibrary
{
    public const string LobeComputeShaderSource = """
#version 450

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

struct RuleEntry {
    int op;
    int operand;
    int array_index;
    float value;
};

layout(set = 0, binding = 0, std430) restrict buffer NeuronBuffer {
    float neuron_states[];
};

layout(set = 0, binding = 1, std430) restrict readonly buffer InputBuffer {
    float neuron_inputs[];
};

layout(set = 0, binding = 2, std430) restrict readonly buffer InitRuleBuffer {
    RuleEntry init_rule[];
};

layout(set = 0, binding = 3, std430) restrict readonly buffer UpdateRuleBuffer {
    RuleEntry update_rule[];
};

layout(set = 0, binding = 4, std430) restrict readonly buffer ChemicalBuffer {
    float chemicals[];
};

layout(set = 0, binding = 5, std430) restrict buffer ResultBuffer {
    int result[];
};

layout(set = 0, binding = 6, std430) restrict buffer InvalidBuffer {
    float invalid_vars[];
};

layout(push_constant, std430) uniform Params {
    int neuron_count;
    int run_init;
    int chemical_count;
    int reserved;
} params;

float bound01(float v) {
    return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
}

float bound_mp1(float v) {
    return v < -1.0 ? -1.0 : (v > 1.0 ? 1.0 : v);
}

int to_int(float v) {
    return int(v * 248.0);
}

int var_index(int i) {
    int m = i % 8;
    return m < 0 ? m + 8 : m;
}

float get_neuron(int neuron_id, int variable) {
    return neuron_states[neuron_id * 8 + var_index(variable)];
}

void set_neuron(int neuron_id, int variable, float value) {
    neuron_states[neuron_id * 8 + var_index(variable)] = value;
}

float get_spare(int spare_index, int variable, inout float dummy_spare[8]) {
    int idx = var_index(variable);
    return spare_index >= 0 ? get_neuron(spare_index, idx) : dummy_spare[idx];
}

void set_spare(int spare_index, int variable, float value, inout float dummy_spare[8]) {
    int idx = var_index(variable);
    if (spare_index >= 0) {
        set_neuron(spare_index, idx, value);
    } else {
        dummy_spare[idx] = value;
    }
}

RuleEntry get_rule_entry(int rule_kind, int index) {
    return rule_kind == 0 ? init_rule[index] : update_rule[index];
}

float read_operand(RuleEntry entry, float accumulator, int neuron_id, int spare_index, inout float dummy_spare[8]) {
    int ai = entry.array_index;
    int idx = var_index(ai);
    if (entry.operand == 0) return accumulator;
    if (entry.operand == 1) return invalid_vars[idx];
    if (entry.operand == 2) return invalid_vars[idx];
    if (entry.operand == 3) return get_neuron(neuron_id, idx);
    if (entry.operand == 4) return get_spare(spare_index, idx, dummy_spare);
    if (entry.operand == 5) return 0.0;
    if (entry.operand == 6) return chemicals[(ai + neuron_id) % params.chemical_count];
    if (entry.operand == 7) return chemicals[ai % params.chemical_count];
    if (entry.operand == 8) return chemicals[(ai + neuron_id) % params.chemical_count];
    if (entry.operand == 9) return 0.0;
    if (entry.operand == 10) return 1.0;
    if (entry.operand == 11) return entry.value;
    if (entry.operand == 12) return -entry.value;
    if (entry.operand == 13) return entry.value * 10.0;
    if (entry.operand == 14) return entry.value / 10.0;
    if (entry.operand == 15) return float(int(entry.value * 248.0));
    return 0.0;
}

void write_dest(RuleEntry entry, float value, int neuron_id, int spare_index, inout float dummy_spare[8]) {
    int idx = var_index(entry.array_index);
    if (entry.operand == 1 || entry.operand == 2) {
        invalid_vars[idx] = value;
    } else if (entry.operand == 3) {
        set_neuron(neuron_id, idx, value);
    } else if (entry.operand == 4) {
        set_spare(spare_index, idx, value, dummy_spare);
    }
}

bool is_no_operand_op(int op) {
    return op == 0 || op == 30 || op == 31 || op == 42;
}

bool is_write_op(int op) {
    return op == 1 || op == 2 || op == 34 || op == 35 || op == 45;
}

void goto_forward(inout int i, float operand) {
    int target = to_int(operand) - 1;
    if (target > i && target <= 16) {
        i = target - 1;
    }
}

int process_rule(int rule_kind, int neuron_id, inout int spare_index, inout float dummy_spare[8]) {
    float accumulator = invalid_vars[0];
    float tend_rate = 0.0;
    int rc = 0;

    for (int i = 0; i < 16; i++) {
        RuleEntry entry = get_rule_entry(rule_kind, i);
        int op = entry.op;

        if (is_no_operand_op(op)) {
            if (op == 0) return rc;
            if (op == 30) {
            } else if (op == 31) {
                rc = 1;
            } else if (op == 42) {
                if (get_neuron(neuron_id, 0) >= get_spare(spare_index, 0, dummy_spare)) {
                    set_spare(spare_index, 2, 0.0, dummy_spare);
                    set_neuron(neuron_id, 2, get_neuron(neuron_id, 0));
                    rc = 1;
                }
            }
            continue;
        }

        if (is_write_op(op)) {
            int idx = var_index(entry.array_index);
            float current = 0.0;
            if (entry.operand == 1 || entry.operand == 2) current = invalid_vars[idx];
            else if (entry.operand == 3) current = get_neuron(neuron_id, idx);
            else if (entry.operand == 4) current = get_spare(spare_index, idx, dummy_spare);

            if (op == 2) write_dest(entry, bound_mp1(accumulator), neuron_id, spare_index, dummy_spare);
            else if (op == 34) write_dest(entry, bound_mp1(accumulator + current), neuron_id, spare_index, dummy_spare);
            else if (op == 1) write_dest(entry, 0.0, neuron_id, spare_index, dummy_spare);
            else if (op == 35) write_dest(entry, bound_mp1(accumulator * (1.0 - tend_rate) + current * tend_rate), neuron_id, spare_index, dummy_spare);
            else if (op == 45) write_dest(entry, bound01(abs(accumulator)), neuron_id, spare_index, dummy_spare);
            continue;
        }

        float operand = entry.operand == 0
            ? accumulator
            : read_operand(entry, accumulator, neuron_id, spare_index, dummy_spare);

        if (op == 3) accumulator = operand;
        else if (op == 4) { if (accumulator != operand) i++; }
        else if (op == 5) { if (accumulator == operand) i++; }
        else if (op == 6) { if (accumulator <= operand) i++; }
        else if (op == 7) { if (accumulator >= operand) i++; }
        else if (op == 8) { if (accumulator < operand) i++; }
        else if (op == 9) { if (accumulator > operand) i++; }
        else if (op == 10) { if (operand != 0.0) i++; }
        else if (op == 11) { if (operand == 0.0) i++; }
        else if (op == 12) { if (operand <= 0.0) i++; }
        else if (op == 13) { if (operand >= 0.0) i++; }
        else if (op == 14) { if (operand < 0.0) i++; }
        else if (op == 15) { if (operand > 0.0) i++; }
        else if (op == 46) { if (operand == 0.0) return rc; }
        else if (op == 47) { if (operand != 0.0) return rc; }
        else if (op == 53) { if (accumulator < operand) return rc; }
        else if (op == 54) { if (accumulator > operand) return rc; }
        else if (op == 55) { if (accumulator <= operand) return rc; }
        else if (op == 56) { if (accumulator >= operand) return rc; }
        else if (op == 48) { if (accumulator == 0.0) goto_forward(i, operand); }
        else if (op == 49) { if (accumulator != 0.0) goto_forward(i, operand); }
        else if (op == 67) { if (accumulator < 0.0) goto_forward(i, operand); }
        else if (op == 68) { if (accumulator > 0.0) goto_forward(i, operand); }
        else if (op == 52) { goto_forward(i, operand); }
        else if (op == 16) accumulator += operand;
        else if (op == 17) accumulator -= operand;
        else if (op == 18) accumulator = operand - accumulator;
        else if (op == 19) accumulator *= operand;
        else if (op == 20) { if (operand != 0.0) accumulator /= operand; }
        else if (op == 21) { if (accumulator != 0.0) accumulator = operand / accumulator; }
        else if (op == 23) { if (operand > accumulator) accumulator = operand; }
        else if (op == 22) { if (operand < accumulator) accumulator = operand; }
        else if (op == 24) tend_rate = abs(operand);
        else if (op == 25) accumulator = accumulator * (1.0 - tend_rate) + operand * tend_rate;
        else if (op == 26) accumulator = -operand;
        else if (op == 27) accumulator = abs(operand);
        else if (op == 28) accumulator = abs(accumulator - operand);
        else if (op == 29) accumulator = operand - accumulator;
        else if (op == 32) accumulator = bound01(operand);
        else if (op == 33) accumulator = bound_mp1(operand);
        else if (op == 36) { if (get_neuron(neuron_id, 1) < operand) set_neuron(neuron_id, 1, 0.0); }
        else if (op == 37) tend_rate = operand;
        else if (op == 38) set_neuron(neuron_id, 1, get_neuron(neuron_id, 1) * (1.0 - tend_rate) + operand * tend_rate);
        else if (op == 39) set_neuron(neuron_id, 1, get_neuron(neuron_id, 1) * operand);
        else if (op == 40) set_neuron(neuron_id, 0, get_neuron(neuron_id, 1) * (1.0 - operand) + get_neuron(neuron_id, 0) * operand);
        else if (op == 41) set_neuron(neuron_id, 0, get_neuron(neuron_id, 0));
        else if (op == 50) { if (operand != 0.0) { accumulator /= operand; set_neuron(neuron_id, 1, bound_mp1(get_neuron(neuron_id, 1) + accumulator)); } }
        else if (op == 51) { accumulator *= operand; set_neuron(neuron_id, 1, bound_mp1(get_neuron(neuron_id, 1) + accumulator)); }
        else if (op == 63) set_neuron(neuron_id, 4, get_neuron(neuron_id, var_index(to_int(operand))));
        else if (op == 64) set_neuron(neuron_id, var_index(to_int(operand)), get_neuron(neuron_id, 4));
        else if (op == 65) set_spare(spare_index, 4, get_spare(spare_index, var_index(to_int(operand)), dummy_spare), dummy_spare);
        else if (op == 66) set_spare(spare_index, var_index(to_int(operand)), get_spare(spare_index, 4, dummy_spare), dummy_spare);
    }

    return rc;
}

void main() {
    if (gl_GlobalInvocationID.x != 0) {
        return;
    }

    float dummy_spare[8];
    for (int i = 0; i < 8; i++) {
        dummy_spare[i] = 0.0;
    }

    int spare_index = -1;
    int winning = 0;

    for (int neuron_id = 0; neuron_id < params.neuron_count; neuron_id++) {
        invalid_vars[0] = neuron_inputs[neuron_id];

        bool flag_as_spare = false;
        if (params.run_init != 0) {
            if (process_rule(0, neuron_id, spare_index, dummy_spare) == 1) {
                flag_as_spare = true;
            }
        }

        if (process_rule(1, neuron_id, spare_index, dummy_spare) == 1) {
            flag_as_spare = true;
        }

        if (flag_as_spare) {
            spare_index = neuron_id;
            winning = neuron_id;
        }
    }

    result[0] = spare_index >= 0 ? winning : 0;
}
""";

    public const string TractComputeShaderSource = """
#version 450

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

struct RuleEntry {
    int op;
    int operand;
    int array_index;
    float value;
};

layout(set = 0, binding = 0, std430) restrict buffer SourceBuffer {
    float source_states[];
};

layout(set = 0, binding = 1, std430) restrict buffer DestinationBuffer {
    float destination_states[];
};

layout(set = 0, binding = 2, std430) restrict buffer WeightBuffer {
    float dendrite_weights[];
};

layout(set = 0, binding = 3, std430) restrict readonly buffer SourceIdBuffer {
    int source_ids[];
};

layout(set = 0, binding = 4, std430) restrict readonly buffer DestinationIdBuffer {
    int destination_ids[];
};

layout(set = 0, binding = 5, std430) restrict readonly buffer InitRuleBuffer {
    RuleEntry init_rule[];
};

layout(set = 0, binding = 6, std430) restrict readonly buffer UpdateRuleBuffer {
    RuleEntry update_rule[];
};

layout(set = 0, binding = 7, std430) restrict readonly buffer ChemicalBuffer {
    float chemicals[];
};

layout(set = 0, binding = 8, std430) restrict buffer ResultBuffer {
    float result[];
};

layout(push_constant, std430) uniform Params {
    int dendrite_count;
    int source_count;
    int destination_count;
    int chemical_count;
    int run_init;
    int spare_id;
    int reserved0;
    int reserved1;
} params;

float bound01(float v) {
    return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
}

float bound_mp1(float v) {
    return v < -1.0 ? -1.0 : (v > 1.0 ? 1.0 : v);
}

int to_int(float v) {
    return int(v * 248.0);
}

int var_index(int i) {
    int m = i % 8;
    return m < 0 ? m + 8 : m;
}

float get_source(int neuron_id, int variable) {
    int idx = neuron_id * 8 + var_index(variable);
    return neuron_id >= 0 && neuron_id < params.source_count ? source_states[idx] : 0.0;
}

void set_source(int neuron_id, int variable, float value) {
    if (neuron_id >= 0 && neuron_id < params.source_count) {
        source_states[neuron_id * 8 + var_index(variable)] = value;
    }
}

float get_destination(int neuron_id, int variable) {
    int idx = neuron_id * 8 + var_index(variable);
    return neuron_id >= 0 && neuron_id < params.destination_count ? destination_states[idx] : 0.0;
}

void set_destination(int neuron_id, int variable, float value) {
    if (neuron_id >= 0 && neuron_id < params.destination_count) {
        destination_states[neuron_id * 8 + var_index(variable)] = value;
    }
}

float get_weight(int dendrite_id, int variable) {
    return dendrite_weights[dendrite_id * 8 + var_index(variable)];
}

void set_weight(int dendrite_id, int variable, float value) {
    dendrite_weights[dendrite_id * 8 + var_index(variable)] = value;
}

float get_spare(int variable) {
    return get_source(params.spare_id, variable);
}

void set_spare(int variable, float value) {
    set_source(params.spare_id, variable, value);
}

RuleEntry get_rule_entry(int rule_kind, int index) {
    return rule_kind == 0 ? init_rule[index] : update_rule[index];
}

float read_operand(RuleEntry entry, float accumulator, int dendrite_id, int source_id, int destination_id) {
    int ai = entry.array_index;
    int idx = var_index(ai);
    if (entry.operand == 0) return accumulator;
    if (entry.operand == 1) return get_source(source_id, idx);
    if (entry.operand == 2) return get_weight(dendrite_id, idx);
    if (entry.operand == 3) return get_destination(destination_id, idx);
    if (entry.operand == 4) return get_spare(idx);
    if (entry.operand == 5) return 0.0;
    if (entry.operand == 6) return chemicals[(ai + source_id) % params.chemical_count];
    if (entry.operand == 7) return chemicals[ai % params.chemical_count];
    if (entry.operand == 8) return chemicals[(ai + destination_id) % params.chemical_count];
    if (entry.operand == 9) return 0.0;
    if (entry.operand == 10) return 1.0;
    if (entry.operand == 11) return entry.value;
    if (entry.operand == 12) return -entry.value;
    if (entry.operand == 13) return entry.value * 10.0;
    if (entry.operand == 14) return entry.value / 10.0;
    if (entry.operand == 15) return float(int(entry.value * 248.0));
    return 0.0;
}

void write_dest(RuleEntry entry, float value, int dendrite_id, int source_id, int destination_id) {
    int idx = var_index(entry.array_index);
    if (entry.operand == 1) {
        set_source(source_id, idx, value);
    } else if (entry.operand == 2) {
        set_weight(dendrite_id, idx, value);
    } else if (entry.operand == 3) {
        set_destination(destination_id, idx, value);
    } else if (entry.operand == 4) {
        set_spare(idx, value);
    }
}

bool is_no_operand_op(int op) {
    return op == 0 || op == 30 || op == 31 || op == 42;
}

bool is_write_op(int op) {
    return op == 1 || op == 2 || op == 34 || op == 35 || op == 45;
}

void goto_forward(inout int i, float operand) {
    int target = to_int(operand) - 1;
    if (target > i && target <= 16) {
        i = target - 1;
    }
}

void process_rule(int rule_kind, int dendrite_id, int source_id, int destination_id, inout float st_to_lt_rate) {
    float accumulator = get_source(source_id, 0);
    float tend_rate = 0.0;

    for (int i = 0; i < 16; i++) {
        RuleEntry entry = get_rule_entry(rule_kind, i);
        int op = entry.op;

        if (is_no_operand_op(op)) {
            if (op == 0) return;
            if (op == 30) {
            } else if (op == 31) {
            } else if (op == 42) {
                if (get_destination(destination_id, 0) >= get_spare(0)) {
                    set_spare(2, 0.0);
                    set_destination(destination_id, 2, get_destination(destination_id, 0));
                }
            }
            continue;
        }

        if (is_write_op(op)) {
            int idx = var_index(entry.array_index);
            float current = 0.0;
            if (entry.operand == 1) current = get_source(source_id, idx);
            else if (entry.operand == 2) current = get_weight(dendrite_id, idx);
            else if (entry.operand == 3) current = get_destination(destination_id, idx);
            else if (entry.operand == 4) current = get_spare(idx);

            if (op == 2) write_dest(entry, bound_mp1(accumulator), dendrite_id, source_id, destination_id);
            else if (op == 34) write_dest(entry, bound_mp1(accumulator + current), dendrite_id, source_id, destination_id);
            else if (op == 1) write_dest(entry, 0.0, dendrite_id, source_id, destination_id);
            else if (op == 35) write_dest(entry, bound_mp1(accumulator * (1.0 - tend_rate) + current * tend_rate), dendrite_id, source_id, destination_id);
            else if (op == 45) write_dest(entry, bound01(abs(accumulator)), dendrite_id, source_id, destination_id);
            continue;
        }

        float operand = entry.operand == 0
            ? accumulator
            : read_operand(entry, accumulator, dendrite_id, source_id, destination_id);

        if (op == 3) accumulator = operand;
        else if (op == 4) { if (accumulator != operand) i++; }
        else if (op == 5) { if (accumulator == operand) i++; }
        else if (op == 6) { if (accumulator <= operand) i++; }
        else if (op == 7) { if (accumulator >= operand) i++; }
        else if (op == 8) { if (accumulator < operand) i++; }
        else if (op == 9) { if (accumulator > operand) i++; }
        else if (op == 10) { if (operand != 0.0) i++; }
        else if (op == 11) { if (operand == 0.0) i++; }
        else if (op == 12) { if (operand <= 0.0) i++; }
        else if (op == 13) { if (operand >= 0.0) i++; }
        else if (op == 14) { if (operand < 0.0) i++; }
        else if (op == 15) { if (operand > 0.0) i++; }
        else if (op == 46) { if (operand == 0.0) return; }
        else if (op == 47) { if (operand != 0.0) return; }
        else if (op == 53) { if (accumulator < operand) return; }
        else if (op == 54) { if (accumulator > operand) return; }
        else if (op == 55) { if (accumulator <= operand) return; }
        else if (op == 56) { if (accumulator >= operand) return; }
        else if (op == 48) { if (accumulator == 0.0) goto_forward(i, operand); }
        else if (op == 49) { if (accumulator != 0.0) goto_forward(i, operand); }
        else if (op == 67) { if (accumulator < 0.0) goto_forward(i, operand); }
        else if (op == 68) { if (accumulator > 0.0) goto_forward(i, operand); }
        else if (op == 52) { goto_forward(i, operand); }
        else if (op == 16) accumulator += operand;
        else if (op == 17) accumulator -= operand;
        else if (op == 18) accumulator = operand - accumulator;
        else if (op == 19) accumulator *= operand;
        else if (op == 20) { if (operand != 0.0) accumulator /= operand; }
        else if (op == 21) { if (accumulator != 0.0) accumulator = operand / accumulator; }
        else if (op == 23) { if (operand > accumulator) accumulator = operand; }
        else if (op == 22) { if (operand < accumulator) accumulator = operand; }
        else if (op == 24) tend_rate = abs(operand);
        else if (op == 25) accumulator = accumulator * (1.0 - tend_rate) + operand * tend_rate;
        else if (op == 26) accumulator = -operand;
        else if (op == 27) accumulator = abs(operand);
        else if (op == 28) accumulator = abs(accumulator - operand);
        else if (op == 29) accumulator = operand - accumulator;
        else if (op == 32) accumulator = bound01(operand);
        else if (op == 33) accumulator = bound_mp1(operand);
        else if (op == 36) { if (get_destination(destination_id, 1) < operand) set_destination(destination_id, 1, 0.0); }
        else if (op == 37) tend_rate = operand;
        else if (op == 38) set_destination(destination_id, 1, get_destination(destination_id, 1) * (1.0 - tend_rate) + operand * tend_rate);
        else if (op == 39) set_destination(destination_id, 1, get_destination(destination_id, 1) * operand);
        else if (op == 40) set_destination(destination_id, 0, get_destination(destination_id, 1) * (1.0 - operand) + get_destination(destination_id, 0) * operand);
        else if (op == 41) set_destination(destination_id, 0, get_destination(destination_id, 0));
        else if (op == 43) st_to_lt_rate = abs(operand);
        else if (op == 44) {
            float old_stw = get_weight(dendrite_id, 0);
            float old_ltw = get_weight(dendrite_id, 1);
            set_weight(dendrite_id, 0, old_stw + (old_ltw - old_stw) * st_to_lt_rate);
            set_weight(dendrite_id, 1, old_ltw + (old_stw - old_ltw) * operand);
        }
        else if (op == 50) { if (operand != 0.0) { accumulator /= operand; set_destination(destination_id, 1, bound_mp1(get_destination(destination_id, 1) + accumulator)); } }
        else if (op == 51) { accumulator *= operand; set_destination(destination_id, 1, bound_mp1(get_destination(destination_id, 1) + accumulator)); }
        else if (op == 63) set_destination(destination_id, 4, get_destination(destination_id, var_index(to_int(operand))));
        else if (op == 64) set_destination(destination_id, var_index(to_int(operand)), get_destination(destination_id, 4));
        else if (op == 65) set_spare(4, get_spare(var_index(to_int(operand))));
        else if (op == 66) set_spare(var_index(to_int(operand)), get_spare(4));
    }
}

void main() {
    if (gl_GlobalInvocationID.x != 0) {
        return;
    }

    float st_to_lt_rate = result[0];
    for (int dendrite_id = 0; dendrite_id < params.dendrite_count; dendrite_id++) {
        int source_id = source_ids[dendrite_id];
        int destination_id = destination_ids[dendrite_id];
        if (params.run_init != 0) {
            process_rule(0, dendrite_id, source_id, destination_id, st_to_lt_rate);
        }
        process_rule(1, dendrite_id, source_id, destination_id, st_to_lt_rate);
    }
    result[0] = st_to_lt_rate;
}
""";
}