#!/bin/zsh

setopt pipefail

count=${1:-5}
project=${2:-"AcaTime.ScheduleGenerator/AcaTime.ScheduleGenerator.csproj"}

if ! [[ "$count" =~ '^[0-9]+$' ]] || (( count < 1 )); then
    print "Usage: $0 [runs] [project-path]"
    exit 2
fi

if [[ ! -f "$project" ]]; then
    print "Project not found: $project"
    exit 2
fi

project_path="${project:A}"
project_directory="${project_path:h}"

timestamp=$(date +%Y%m%d-%H%M%S)
output_dir="benchmark-results/$timestamp"
mkdir -p "$output_dir"
summary="$output_dir/summary.csv"

print "run,default_score,best_genetic_score,delta,percent,duration_seconds,exit_code" > "$summary"
print "Running $count Genetic benchmark(s)"
print "Logs: $output_dir"
print ""

for run in {1..$count}; do
    log_file="$output_dir/run-$run.log"
    started_at=$(date +%s)

    print "[$run/$count] starting..."
    (
        cd "$project_directory" || exit 1
        dotnet run --project "$project_path" --no-build --no-restore
    ) > "$log_file" 2>&1 &
    process_id=$!
    while kill -0 "$process_id" 2>/dev/null; do
        sleep 10
        elapsed=$(( $(date +%s) - started_at ))
        if kill -0 "$process_id" 2>/dev/null; then
            print "[$run/$count] still running (${elapsed}s)..."
        fi
    done
    wait "$process_id"
    exit_code=$?

    finished_at=$(date +%s)
    duration=$((finished_at - started_at))

    default_score=$(awk '
        /Збереження розкладу Default з [0-9]+/ {
            if (match($0, /Default з [0-9]+/)) {
                value = substr($0, RSTART, RLENGTH)
                sub(/^Default з /, "", value)
                if ((value + 0) > best) best = value + 0
            }
        }
        END { if (best > 0) print best }
    ' "$log_file")

    genetic_score=$(awk '
        /Збереження розкладу Genetic з [0-9]+/ {
            if (match($0, /Genetic з [0-9]+/)) {
                value = substr($0, RSTART, RLENGTH)
                sub(/^Genetic з /, "", value)
                if ((value + 0) > best) best = value + 0
            }
        }
        END { if (best > 0) print best }
    ' "$log_file")

    if [[ -n "$default_score" && -n "$genetic_score" ]]; then
        delta=$((genetic_score - default_score))
        percent=$(awk -v delta="$delta" -v base="$default_score" 'BEGIN { printf "%.4f", (delta / base) * 100 }')
    else
        delta=""
        percent=""
    fi

    print "$run,$default_score,$genetic_score,$delta,$percent,$duration,$exit_code" >> "$summary"
    print "[$run/$count] Default=${default_score:-N/A} Genetic=${genetic_score:-N/A} Delta=${delta:-N/A} (${percent:-N/A}%) Duration=${duration}s Exit=$exit_code"
done

print ""
print "Summary: $summary"
print ""
awk -F, '
    NR > 1 && $4 != "" {
        runs++
        sum += $4
        if ($4 > best_delta) best_delta = $4
        if ($4 < worst_delta || runs == 1) worst_delta = $4
        if ($4 > 0) improvements++
    }
    END {
        if (runs == 0) {
            print "No comparable Default/Genetic results found."
            exit
        }
        printf "Comparable runs: %d\n", runs
        printf "Improved runs: %d/%d\n", improvements, runs
        printf "Average delta: %.2f\n", sum / runs
        printf "Best delta: %.0f\n", best_delta
        printf "Worst delta: %.0f\n", worst_delta
    }
' "$summary"
